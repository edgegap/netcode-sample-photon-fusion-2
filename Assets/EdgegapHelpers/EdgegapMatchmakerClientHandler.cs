using System;
using System.Collections.Generic;
using Edgegap.Gen2SDK;
using Fusion;
using Fusion.Sockets;
using UnityEngine;
using UnityEngine.Networking;
using MyTicketsAttributes = Edgegap.Gen2SDK.LatenciesAttributesDTO;
using MyTicketsRequestDTO = Edgegap.Gen2SDK.SimpleTicketsRequestDTO;

public class EdgegapMatchmakerClientHandler : MonoBehaviour
{
    public static EdgegapMatchmakerClientHandler Instance { get; private set; }
    public static bool EdgegapMode = false;

    #region Gen2Client Configuration
    public string BaseUrl;
    public string AuthToken;

    public string ClientVersion = "1.0.0";
    public bool SaveStateInPlayerPrefs = true;
    public string PLAYER_PREFS_KEY_VERSION = "EdgegapGen2ClientVersion";
    public string PLAYER_PREFS_KEY_TICKET = "EdgegapGen2ClientTicket";
    public string PLAYER_PREFS_KEY_ASSIGNMENT = "EdgegapGen2ClientAssignment";

    public int RequestTimeoutSeconds = 3;
    public float PollingBackoffSeconds = 1f;
    public int MaxConsecutivePollingErrors = 10;

    public float RemoveAssignmentSeconds = 30f;
    public bool DeleteTicketOnPause = false;
    public bool DeleteTicketOnQuit = true;

    public bool LogTicketUpdates = true;
    public bool LogAssignmentUpdates = true;
    public bool LogPollingUpdates = false;
    #endregion

    public Client<MyTicketsRequestDTO, MyTicketsAttributes> Gen2Client;

    private string State;

    public void Awake()
    {
        // If there is an instance, and it's not me, delete myself.

        if (Instance != null && Instance != this)
        {
            Destroy(this);
        }
        else
        {
            Instance = this;
        }
    }

    public void InitialiseClient(Action<GameMode, string, string, NetAddress?> onHostAssigned, Action<string> updateUI)
    {
        // configure Gen2
        Gen2Client = new Client<MyTicketsRequestDTO, MyTicketsAttributes>(
            this,
            BaseUrl,
            AuthToken,
            ClientVersion,
            SaveStateInPlayerPrefs,
            PLAYER_PREFS_KEY_VERSION,
            PLAYER_PREFS_KEY_TICKET,
            PLAYER_PREFS_KEY_ASSIGNMENT,
            RequestTimeoutSeconds,
            PollingBackoffSeconds,
            MaxConsecutivePollingErrors,
            RemoveAssignmentSeconds,
            LogTicketUpdates,
            LogAssignmentUpdates,
            LogPollingUpdates
        );

        // initialize Gen2
        Gen2Client.Initialize(
            // handle service monitoring
            (
                Observable<MonitorResponseDTO> monitor,
                ObservableActionType action,
                string message
            ) =>
            {
                if (action == ObservableActionType.Update)
                {
                    if (State is null && message == "healthy")
                    {
                        // todo update UI
                        Gen2Client.ResumeMatchmaking();
                    }
                    else if (message != "healthy")
                    {
                        // todo handle outage/maintenance
                        Debug.LogError($"Gen2 error.\n{monitor.Current}");
                        Gen2Client.StopMatchmaking();
                    }
                }
            },
            // handle ticket assignment
            (
                Observable<TicketResponseDTO> assignment,
                ObservableActionType action,
                string message
            ) =>
            {
                if (action == ObservableActionType.Log && message.Contains("restart suggested"))
                {
                    Gen2Client.Beacons(
                        (BeaconsResponseDTO beacons) =>
                        {
                            Debug.Log($"beacons: {beacons}");

                            Gen2Client.MeasureBeaconsRoundTripTime(
                                beacons.Beacons,
                                (Dictionary<string, float> pings) =>
                                    Gen2Client.StartMatchmaking(new MyTicketsRequestDTO(pings))
                            );
                        },
                        (string error, UnityWebRequest request) =>
                        {
                            // todo handle beacon downtime, create tickets without beacons?
                            Debug.Log($"beacon error: {request}");
                        }
                    );
                }
                else if (
                    action == ObservableActionType.Update
                    && (
                        message.Contains("received")
                        || message.Contains("updated")
                        || message.Contains("abandoned")
                    )
                )
                {
                    // update UI
                    updateUI(message);
                }

                if (
                    (
                        action == ObservableActionType.Update
                        && message.Contains("updated")
                        && assignment.Current.Status == "HOST_ASSIGNED"
                    )
                    || (
                        action == ObservableActionType.Log
                        && message.Contains("reconnect suggested")
                    )
                )
                {
                    // join game
                    onHostAssigned(GameMode.Client, assignment.Current.Assignment.Fqdn, "AsteroidsSimple-Game", null);
                }
            }
        );
    }

    public void OnApplicationPause(bool pause)
    {
        if (!DeleteTicketOnPause || Gen2Client.Ticket.Current is null)
            return;
        StopMatchmaking();
    }

    public void OnApplicationQuit()
    {
        if (!DeleteTicketOnQuit || Gen2Client is null)
            return;
        StopMatchmaking();
    }

    public void StartMatchmaking(MyTicketsRequestDTO ticket)
    {
        Gen2Client.StartMatchmaking(ticket);
    }

    // group members need to share tickets to group host to start matchmaking
    public void StartGroupMatchmaking(
        MyTicketsRequestDTO hostTicket,
        List<MyTicketsRequestDTO> memberTickets,
        bool abandon = false
    )
    {
        Gen2Client.StartGroupMatchmaking(
            hostTicket,
            memberTickets,
            (List<TicketResponseDTO> memberAssignments, UnityWebRequest request) =>
            {
                // todo send assignment IDs to group members to track their tickets
                Debug.Log($"member assignemnts: {memberAssignments}");
            },
            abandon
        );
    }

    public void StopMatchmaking()
    {
        Gen2Client.StopMatchmaking();
    }
}
