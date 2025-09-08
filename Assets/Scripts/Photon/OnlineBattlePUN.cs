using System.Collections.Generic;
using UnityEngine;
using Photon.Pun;
using Photon.Realtime;

public class OnlineBattlePUN : MonoBehaviourPunCallbacks
{
    [Header("Refs")]
    public BattleManager battle;   // ← インスペクタで割当
    public SpellBook spellBook;    // ← インスペクタで割当

    // Masterが持つ“真の状態”
    private int p1Hp = 20, p2Hp = 20, turn = 1;

    // Masterが受け付けた選択（ActorNumber→(A,B)）
    private readonly Dictionary<int, (Element, Element)> picks = new Dictionary<int, (Element, Element)>();

    private int localActor;   // 自分のActorNumber
    private int p1Actor, p2Actor; // P1/P2のActorNumber（小さい方をP1にする固定ルール）

    [SerializeField] private PhotonView pv;

    void Start()
    {
        localActor = PhotonNetwork.LocalPlayer.ActorNumber;
        RefreshP1P2(); // 入室順に応じてP1/P2を確定（2人そろったら決まる）

        // BattleManager へ自分を割り当て（オンライン経路を使えるように）
        if (battle != null) battle.net = this;
    }

    void RefreshP1P2()
    {
        var list = new List<Player>(PhotonNetwork.PlayerList);
        if (list.Count >= 2)
        {
            list.Sort((a, b) => a.ActorNumber.CompareTo(b.ActorNumber));
            p1Actor = list[0].ActorNumber;
            p2Actor = list[1].ActorNumber;
        }
    }

    // --------- クライアント：自分の選択を送信 ---------
    public void SubmitMyPick(Element a, Element b)
    {
        if (!PhotonNetwork.IsConnected || !PhotonNetwork.InRoom)
        {
            Debug.LogWarning("未接続 or 未入室のため送信しません");
            return;
        }

        var pv = PhotonView.Get(this);
        if (pv == null)
        {
            Debug.LogError("PhotonView がこのオブジェクトにありません（OnlineBattlePUN と同じGOに付けてください）");
            return;
        }

        // その場で正しい ActorNumber を取得
        int actor = (PhotonNetwork.LocalPlayer != null) ? PhotonNetwork.LocalPlayer.ActorNumber : -1;
        if (actor <= 0)
        {
            Debug.LogError("ActorNumber が未取得（<=0）。OnJoinedRoom 後に実行してください。");
            return;
        }

        Debug.Log($"[Client {actor}] 送信 {a}+{b}");
        pv.RPC(nameof(RpcSubmitPickToMaster), RpcTarget.MasterClient, actor, (int)a, (int)b);
    }

    [PunRPC]
    void RpcSubmitPickToMaster(int actor, int a, int b, PhotonMessageInfo info)
    {
        if (!PhotonNetwork.IsMasterClient) return;

        // 受信ログ（デバッグ用）
        Debug.Log($"[Master] pick受信 actor={actor} a={(Element)a} b={(Element)b}");

        // 受理（同じactorが2回押しても上書き）
        picks[actor] = ((Element)a, (Element)b);

        // ★ここを「picks.Count>=2」に変更（人数2人前提の最小実装）
        if (PhotonNetwork.CurrentRoom != null &&
            PhotonNetwork.CurrentRoom.PlayerCount >= 2 &&
            picks.Count >= 2)
        {
            // 受け取った2人を actorNumber でソート → 小さい方をP1に
            var keys = new List<int>(picks.Keys);
            keys.Sort();
            int p1Key = keys[0];
            int p2Key = keys[1];

            // 先で使うため、現在のP1/P2を更新しておく
            p1Actor = p1Key;
            p2Actor = p2Key;

            // 解決
            ResolveOnMaster(picks[p1Key], picks[p2Key]);

            // 次ターン用にクリア
            picks.Clear();
        }
    }

    // --------- Masterの判定ロジック ---------
    void ResolveOnMaster((Element, Element) p1, (Element, Element) p2)
    {
        // スペル化
        var s1 = spellBook.Find(p1.Item1, p1.Item2);
        var s2 = spellBook.Find(p2.Item1, p2.Item2);

        // 先手（交互＋俊足で上書き）
        bool p1First = (turn % 2 == 1);
        bool h1 = s1 && s1.Effects.Exists(e => e.Kind == EffectKind.Haste);
        bool h2 = s2 && s2.Effects.Exists(e => e.Kind == EffectKind.Haste);
        if (h1 ^ h2) p1First = h1;

        // 作業用
        int hp1 = p1Hp, hp2 = p2Hp;
        System.Action<Spell, bool> apply = (sp, isP1) =>
        {
            if (!sp) return;
            foreach (var eff in sp.Effects)
            {
                switch (eff.Kind)
                {
                    case EffectKind.Damage:
                    case EffectKind.Pierce:
                        if (isP1) hp2 = Mathf.Max(0, hp2 - eff.Value); else hp1 = Mathf.Max(0, hp1 - eff.Value);
                        break;
                    case EffectKind.Heal:
                        if (isP1) hp1 = Mathf.Min(battle.maxHp, hp1 + eff.Value); else hp2 = Mathf.Min(battle.maxHp, hp2 + eff.Value);
                        break;
                    case EffectKind.Haste:
                    case EffectKind.Stun:
                        // 最小版は未実装（必要ならNetworkで状態を持つ）
                        break;
                }
            }
        };

        // ログ行（2〜3行）
        string ln1 = $"P1: {(s1 ? s1.DisplayNameJP : "行動不能/不発")} / P2: {(s2 ? s2.DisplayNameJP : "行動不能/不発")}";

        if (p1First) { apply(s1, true); apply(s2, false); }
        else { apply(s2, false); apply(s1, true); }

        p1Hp = hp1; p2Hp = hp2; turn++;

        string result = (hp1 <= 0 && hp2 <= 0) ? "相打ち！引き分け"
                     : (hp1 <= 0) ? "P2の勝利！"
                     : (hp2 <= 0) ? "P1の勝利！"
                     : $"--- ターン {turn} ---";

        // ★全員に「新しいHPとログ」を配信
        photonView.RPC(nameof(RpcApplyTurnOnClients), RpcTarget.All, p1Hp, p2Hp, ln1, result, turn);
    }

    // --------- クライアント：受け取ってUI更新 ---------
    [PunRPC]
    void RpcApplyTurnOnClients(int newP1Hp, int newP2Hp, string line1, string line2, int newTurn)
    {
        // 自分視点に変換（自分がP1ならP1が自分のHP）
        bool iAmP1 = (localActor == p1Actor);

        int my = iAmP1 ? newP1Hp : newP2Hp;
        int enemy = iAmP1 ? newP2Hp : newP1Hp;

        // BattleManagerのUIを更新（ログ＆HP）
        if (battle != null)
        {
            battle.Log(line1);
            battle.Log(line2);
            // 直接書き換え（安全のため専用Setterを作ってもOK）
            typeof(BattleManager).GetField("_playerHp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(battle, my);
            typeof(BattleManager).GetField("_enemyHp", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(battle, enemy);
            typeof(BattleManager).GetField("_turn", System.Reflection.BindingFlags.NonPublic | System.Reflection.BindingFlags.Instance)?.SetValue(battle, newTurn);

            battle.UpdateUI();
            battle.RefreshPickText();
            battle.confirmBtn.interactable = true; // 次ターン入力可
        }
    }



    // 人の出入りでP1/P2を再計算
    public override void OnPlayerEnteredRoom(Player newPlayer) => RefreshP1P2();
    public override void OnPlayerLeftRoom(Player otherPlayer) => RefreshP1P2();
}
