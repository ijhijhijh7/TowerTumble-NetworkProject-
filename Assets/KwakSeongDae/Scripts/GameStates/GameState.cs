using Photon.Pun;
using Photon.Pun.UtilityScripts;
using Photon.Realtime;
using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using WebSocketSharp;

[RequireComponent(typeof(PhotonView))]
public class GameState : MonoBehaviourPunCallbacks
{
    private const int maxPlayer = 4;

    [Header("게임 시작 & 종료 설정")]
    [SerializeField] protected float startDelayTime;
    [SerializeField] protected float finishDelayTime;
    [SerializeField] private PlayerGameCanvasUI uiPrefab;
    [SerializeField] private int returnSceneIndex;

    [Header("플레이어 스폰 설정")]
    [SerializeField] private string playerPrefabPath;
    [SerializeField] private string towerPrefabPath;
    [SerializeField] private string wallPrefabPath;
    [SerializeField] private float maxLeft;                // 스폰 가능 지역의 좌하단 좌표
    [SerializeField] private float maxRight;               // 스폰 가능 지역의 우상단 좌표
    [SerializeField] private float playerSpaceMaxWidth;    // 플레이어 개인 영역 최대치 제한
    [SerializeField] private float initY;

    [HideInInspector] public Dictionary<int, GameObject> playerObjectDic;
    [HideInInspector] public Dictionary<int, GameObject> towerObjectDic;
    [HideInInspector] public float playerWidth;
    protected PlayerGameCanvasUI playerUI;
    private WaitForSecondsRealtime startDelay;
    private WaitForSeconds finishDelay;

    // 활성화 시점에 모두 초기화
    public override void OnEnable()
    {
        base.OnEnable();
        StartCoroutine(NetworkWaitRoutine());
    }

    private void OnDestroy()
    {

        // 방장만 게임 씬 정리 작업 수행
        if (!PhotonNetwork.IsMasterClient) return;

        // 모든 플레이어가 생성한 네트워크 오브젝트들 삭제 (블럭 및 RPC 포함)
        PhotonNetwork.DestroyAll();
        print("네트워크 오브젝트들 전부 삭제");
        playerObjectDic.Clear();
        towerObjectDic.Clear();

    }
    public override void OnDisable()
    {
        // 방장만 게임 씬 로드 작업 수행
        if (PhotonNetwork.IsMasterClient == false) return;

        PhotonNetwork.CurrentRoom.IsOpen = true;
        print("게임이 끝났으므로 씬이 전환됩니다.");
        PhotonNetwork.LoadLevel(returnSceneIndex);

        base.OnDisable();
    }

    protected virtual void Init()
    {
        // 시작 딜레이는 게임이 멈춰야되는 기능도 포함하므로 Realtime으로 계산
        startDelay = new WaitForSecondsRealtime(startDelayTime);
        finishDelay = new WaitForSeconds(finishDelayTime);
        // 플레이어 오브젝트 딕셔너리는 모든 클라이언트가 가질수 있도록 설정
        if (playerObjectDic == null)
            playerObjectDic = new Dictionary<int, GameObject>();
        if (towerObjectDic == null)
            towerObjectDic = new Dictionary<int, GameObject>();

        if (playerPrefabPath.IsNullOrEmpty() == false
            && uiPrefab != null)
        {
            var players = PhotonNetwork.PlayerList;
            var playerSpawnPos = PlayerSpawnStartPositions(initY, players.Length);
            print($"플레이어 수: {players.Length}"); 

            var playerNum = PhotonNetwork.LocalPlayer.GetPlayerNumber();
            // 타워 생성
            print(playerSpawnPos[playerNum]);

            var towerObj = PhotonNetwork.Instantiate(towerPrefabPath, playerSpawnPos[playerNum], Quaternion.identity, data: new object[] { playerNum, photonView.ViewID });
            // 네트워크 플레이어 오브젝트를 생성하기
            var playerPath = playerPrefabPath + playerNum.ToString();
            var playerObj = PhotonNetwork.Instantiate(playerPath, playerSpawnPos[playerNum], Quaternion.identity, data: new object[] { players[playerNum].NickName });

            var playerView = playerObj.GetComponent<PhotonView>();
            var towerView = towerObj.GetComponent<PhotonView>();

            photonView.RPC("SetPlayerObjectDic", RpcTarget.All, playerView.ViewID);
            photonView.RPC("SetTowerObjectDic", RpcTarget.All, towerView.ViewID);
            // 본인 오브젝트가 생성되는 경우에는 본인 UI도 같이 생성
            playerUI = Instantiate(uiPrefab);
            playerUI.GetComponent<PlayerGameCanvasUI>().gameState = gameObject;
        }

        // RPC이용해서 시작 시간 동기화, 방장이 RPC날리기
        if (PhotonNetwork.IsMasterClient)
            photonView.RPC("StartRoutineWrap", RpcTarget.All);
    }
    
    private IEnumerator NetworkWaitRoutine()
    {
        var delay = new WaitForSeconds(1f);
        yield return delay;
        Init();
    }

    [PunRPC]
    protected void SetPlayerObjectDic(int viewID)
    {
        if (playerObjectDic == null)
            playerObjectDic = new Dictionary<int, GameObject>();
        var obj = PhotonView.Find(viewID);
        playerObjectDic.Add(obj.Owner.ActorNumber, obj.gameObject);
    }

    [PunRPC]
    protected void SetTowerObjectDic(int viewID)
    {
        if (towerObjectDic == null)
            towerObjectDic = new Dictionary<int, GameObject>();
        var obj = PhotonView.Find(viewID);
        towerObjectDic.Add(obj.Owner.ActorNumber, obj.gameObject);
    }

    [PunRPC]
    protected void StartRoutineWrap(PhotonMessageInfo info)
    {
        StartCoroutine(StartRoutine(info.SentServerTime));
    }

    /// <summary>
    /// 모드 시작 시, 작동할 타이머 루틴
    /// </summary>
    protected IEnumerator StartRoutine(double startTime)
    {
        var delay = Math.Abs(PhotonNetwork.Time - startTime);
        //print($"방장이 보낸 RPC를 수신까지 딜레이 {delay}");
        // 지연보상 적용
        playerUI?.SetTimer(startDelayTime - (float)delay);
        Time.timeScale = 0f;
        yield return startDelay;
        playerUI?.SetTimer(0);
        Time.timeScale = 1f;
    }

    /// <summary>
    /// 각 게임 모드 별, FinishRoutine 가상 함수
    /// </summary>
    protected virtual IEnumerator FinishRoutine(int playerID)
    {
        playerUI?.SetTimer(finishDelayTime);
        yield return finishDelay;
        playerUI?.SetTimer(0);
    }

    /// <summary>
    /// FinishRoutine종료시, StopCoroutine전에 한번 거쳐가는 미들함수
    /// </summary>
    protected void StopFinishRoutine(Coroutine routine)
    {
        playerUI?.SetTimer(0);
        StopCoroutine(routine);
    }

    /// <summary>
    /// 플레이어가 스폰할 위치 반환 및 블럭 제한구역 설정
    /// </summary>
    /// <param name="bottomLeft"> 맵 좌하단 위치</param>
    /// <param name="upRight"> 맵 우상단 위치</param>
    /// <param name="playerNum"> 총 플레이어 수</param>
    /// <returns></returns>
    private Vector2[] PlayerSpawnStartPositions(float initY,int playerNum)
    {
        if (playerNum < 1 || playerNum > maxPlayer) return null;

        // 개인 플레이어 너비 = 전체 너비 / 플레이어 수
        // 개인 플레이어 영역은 0.25단위로 움직일 수 있도록 조정
        var rawWidth = MathF.Abs(maxRight - maxLeft) / playerNum;
        playerWidth = Mathf.Ceil(rawWidth / 0.5f) * 0.5f;

        // 플레이어 영역은 최대 개인 너비를 벗어나지 못하도록 설정
        if (playerWidth > playerSpaceMaxWidth) playerWidth = playerSpaceMaxWidth;

        // 조정된 width에 따라, 좌하단 좌표 수정, 
        var widthRemain = MathF.Abs(maxRight - maxLeft) - (playerWidth * playerNum);
        // 가운데 정렬
        var bottomLeft = new Vector2(maxLeft + widthRemain / 2, initY);

        // 투명 벽 수 = 플레이어 수 + 1
        // 투명 벽 위치 (x값) = bottomLeft.x + 투명 벽 인덱스 * width
        // 투명 벽 위치 (y값) = bottomLeft.y
        for (int i = 0; i < playerNum + 1; i++)
        {
            PhotonNetwork.Instantiate(wallPrefabPath, new Vector2(bottomLeft.x + (i * playerWidth), initY), Quaternion.identity);
        }

        // 플레이어 스폰 위치 (x값) =
        // (bottomLeft + 개인 너비 * 플레이어 인덱스 = 각 플레이어 영역의 bottomLeft)
        // + (개인너비 / 2 = 각 플레이어 영역의 중심) 
        // 플레이어 스폰 위치 (y값) = bottomLeft.y
        var playerPositions = new Vector2[playerNum];
        for (int i = 0; i < playerPositions.Length; i++)
        {
            playerPositions[i] = new Vector2((bottomLeft.x + playerWidth * i) + (playerWidth / 2), initY);
        }
        return playerPositions;
    }

    public override void OnMasterClientSwitched(Player newMasterClient)
    {
        print("방장이 나감에 따라서 게임이 종료됩니다.");

        // 방장이 바뀌는 경우 (강제종료 된 경우)
        // 1. 게임 씬의 모든 오브젝트 정리 
        if (gameObject != null)
            gameObject.SetActive(false);

        // 2. 방 떠나기
        PhotonNetwork.LeaveRoom();
    }

    public override void OnLeftRoom()
    {
        // 3. 로비 씬으로 리턴
        PhotonNetwork.LoadLevel(returnSceneIndex);
    }
}
