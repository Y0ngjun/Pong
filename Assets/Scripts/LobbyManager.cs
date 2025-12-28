using System.Collections.Generic;
using System.Text;
using Unity.Netcode;
using UnityEngine.SceneManagement;
using UnityEngine.UI;

public class LobbyManager : NetworkBehaviour
{
    // 플레이어 목록과 준비 여부를 표시할 UI 텍스트
    public Text lobbyText;

    // 게임을 시작하기 위해 필요한 최소한의 준비된 플레이어 수
    private const int MinimumReadyCountToStartGame = 2;

    // 플레이어 준비 상태를 저장하는 딕셔너리
    private Dictionary<ulong, bool> _clientReadyStates
        = new Dictionary<ulong, bool>();

    // OnNetworkSpawn은 NetworkBehaviour가 생성될 때 호출됨
    public override void OnNetworkSpawn()
    {
        _clientReadyStates.Add(NetworkManager.LocalClientId, false);

        if (IsServer)
        {
            NetworkManager.OnClientConnectedCallback += OnClientConnected;
            NetworkManager.OnClientDisconnectCallback += OnClientDisconnected;
            NetworkManager.SceneManager.OnLoadComplete += OnClientSceneLoadComplete;
        }

        UpdateLobbyText();
    }

    private void OnDisable()
    {
        if (!IsServer)
        {
            return;
        }

        if (NetworkManager.Singleton != null)
        {
            NetworkManager.OnClientConnectedCallback -= OnClientConnected;
            NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
            NetworkManager.SceneManager.OnLoadComplete -= OnClientSceneLoadComplete;
        }
    }

    // 클라이언트가 연결되었을때 실행할 콜백
    private void OnClientConnected(ulong clientId)
    {
        _clientReadyStates.Add(clientId, false);
        UpdateLobbyText();
    }

    // 클라이언트가 씬 로드를 완료했을 때 실행할 콜백
    private void OnClientSceneLoadComplete(ulong clientId, string sceneName, LoadSceneMode loadSceneMode)
    {
        if (sceneName != "Lobby")
        {
            return;
        }

        foreach (KeyValuePair<ulong, bool> pair in _clientReadyStates)
        {
            var id = pair.Key;
            var isReady = pair.Value;
            SetClientIsReadyClientRpc(id, isReady);
        }
    }

    // 클라이언트 접속이 끊겼을 때 실행할 콜백
    private void OnClientDisconnected(ulong clientId)
    {
        if (_clientReadyStates.ContainsKey(clientId))
        {
            _clientReadyStates.Remove(clientId);
        }

        RemovePlayerClientRpc(clientId);
        UpdateLobbyText();
    }

    // 서버가 다른 클라이언트들에게 어떤 클라이언트의 준비 상태가 변경됨을 알리는 RPC 메서드
    // 서버에서 요청되어 각 클라이언트들에서 실행됨
    [ClientRpc]
    private void SetClientIsReadyClientRpc(ulong clientId, bool isReady)
    {
        if (IsServer)
        {
            return;
        }

        _clientReadyStates[clientId] = isReady;
        UpdateLobbyText();
    }

    // 서버가 다른 클라이언트들에게 어떤 클라이언트의 준비 상태가 변경됨을 알리는 RPC 메서드
    // 서버에서 요청되어 각 클라이언트들에서 실행됨
    [ClientRpc]
    private void RemovePlayerClientRpc(ulong clientId)
    {
        if (IsServer)
        {
            return;
        }

        _clientReadyStates.Remove(clientId);
        UpdateLobbyText();
    }

    // 로비 텍스트를 갱신
    private void UpdateLobbyText()
    {
        var stringBuilder = new StringBuilder();

        foreach (var pair in _clientReadyStates)
        {
            var clientId = pair.Key;
            var isReady = pair.Value;

            if (isReady)
            {
                stringBuilder.AppendLine($"PLAYER_{clientId} : READY");
            }
            else
            {
                stringBuilder.AppendLine($"PLAYER_{clientId} : NOT READY");
            }
        }

        lobbyText.text = stringBuilder.ToString();
    }

    // 게임을 시작할 수 있는지 확인
    private bool CheckIsReadyToStart()
    {
        if (_clientReadyStates.Count < MinimumReadyCountToStartGame)
        {
            return false;
        }

        foreach (var isReady in _clientReadyStates.Values)
        {
            if (!isReady)
            {
                return false;
            }
        }

        return true;
    }

    // 게임 시작
    private void StartGame()
    {
        if (!IsServer)
        {
            return;
        }

        NetworkManager.OnClientConnectedCallback -= OnClientConnected;
        NetworkManager.OnClientDisconnectCallback -= OnClientDisconnected;
        NetworkManager.SceneManager.OnLoadComplete -= OnClientSceneLoadComplete;

        NetworkManager.SceneManager.LoadScene("InGame", LoadSceneMode.Single);
    }

    // 클라이언트가 준비 버튼을 눌렀을때 실행하는 메서드
    public void SetPlayerIsReady()
    {
        var localClientId = NetworkManager.LocalClientId;

        var isReady = !_clientReadyStates[localClientId];
        _clientReadyStates[localClientId] = isReady;

        UpdateLobbyText();

        if (IsServer)
        {
            SetClientIsReadyClientRpc(localClientId, isReady);

            if (CheckIsReadyToStart())
            {
                StartGame();
            }
        }
        else
        {
            SetClientIsReadyServerRpc(localClientId, isReady);
        }
    }

    // 클라이언트가 준비 상태가 변경됬음을 서버에게 알리기 위한 RPC 메서드
    // 클라이언트에서 요청되어 서버에서 실행됨
    [ServerRpc(RequireOwnership = false)]
    private void SetClientIsReadyServerRpc(ulong clientId, bool isReady)
    {
        _clientReadyStates[clientId] = isReady;
        SetClientIsReadyClientRpc(clientId, isReady);
        UpdateLobbyText();

        if (CheckIsReadyToStart())
        {
            StartGame();
        }
    }
}