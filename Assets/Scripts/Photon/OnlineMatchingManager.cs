using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class OnlineMatchingManager : MonoBehaviourPunCallbacks
{
    bool isEnterRoom; // 部屋に入ってるかどうかのフラグ
    bool isMatching; // マッチング済みかどうかのフラグ

    public void OnMatchingButton()
    {
        // PhotonServerSettingsの設定内容を使ってマスターサーバーへ接続する
        PhotonNetwork.ConnectUsingSettings();
    }

    // マスターサーバーへの接続が成功した時に呼ばれるコールバック
    public override void OnConnectedToMaster()
    {
        // ランダムマッチング
        PhotonNetwork.JoinRandomRoom();
    }

    // ゲームサーバーへの接続が成功した時に呼ばれるコールバック
    public override void OnJoinedRoom()
    {
        isEnterRoom = true;
    }

    // 失敗した場合
    public override void OnJoinRandomFailed(short returnCode, string message)
    {
        PhotonNetwork.CreateRoom(null, new RoomOptions() { MaxPlayers = 2 }, TypedLobby.Default);
    }

    // もし二人ならシーンを変える
    private void Update()
    {
        if (isMatching) return;

        // ① CurrentRoom の null ガード
        if (!isEnterRoom || PhotonNetwork.CurrentRoom == null)
        {
            // まだ部屋情報が来てないだけ
            return;
        }

        // ② 人数が揃ったか？
        if (PhotonNetwork.CurrentRoom.PlayerCount == PhotonNetwork.CurrentRoom.MaxPlayers)
        {
            isMatching = true;
            Debug.Log($"マッチング成功: PlayerCount={PhotonNetwork.CurrentRoom.PlayerCount} / Max={PhotonNetwork.CurrentRoom.MaxPlayers}");

            // ③ BattleManager がいるか確認
            var bm = BattleManager.instance ?? FindObjectOfType<BattleManager>();
            if (bm == null)
            {
                Debug.LogError("BattleManager が見つかりません。シーンに配置＆Awakeで instance を設定してください。");
                return;
            }

            bm.StartGame();
        }
    }


    [PunRPC]
    void RpcStartGameAll()
    {
        BattleManager.instance.StartGame(); // or FindObjectOfType<BattleManager>().StartGame();
    }
}