using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;
using System.Collections;


public class BattleManager : MonoBehaviour
{
    [Header("Refs")]
    public SpellBook spellBook;

    [Header("UI - Buttons")]
    public Button fireBtn;
    public Button waterBtn;
    public Button windBtn;
    public Button earthBtn;
    public Button confirmBtn;

    [Header("UI - Texts (TMP)")]
    public TMP_Text playerHpText;
    public TMP_Text enemyHpText;
    public TMP_Text playerPickText;
    public TMP_Text logText;

    [Header("Log Scroll")]
    public ScrollRect logScroll;          // ← インスペクタで LogScroll を割り当て
    public int maxLogLines = 200;         // ログの保持上限行数
    private readonly System.Collections.Generic.List<string> _logBuffer = new System.Collections.Generic.List<string>(256);
    private bool _autoScroll = true;

    [Header("Config")]
    public int maxHp = 20;

    // 状態
    private List<Element> _playerPicks = new List<Element>(2);
    private List<Element> _enemyPicks = new List<Element>(2);
    private int _playerHp, _enemyHp;
    private bool _playerStunned = false, _enemyStunned = false;
    private bool _playerHasHaste = false, _enemyHasHaste = false;
    private int _turn = 1; // 奇数:プレイヤー先手 / 偶数:敵先手


    // シングルトン化
    public static BattleManager instance { get; private set; }

    // Photon連携用
    public OnlineBattlePUN net;
    void Awake()
    {
        if (instance != null && instance != this)
        {
            Destroy(gameObject);
            return;
        }
        instance = this;
    }
    void Start()
    {
        // ★ここでは“開始しない”。UIの配線だけ残す
        fireBtn.onClick.AddListener(delegate { OnPick(Element.Fire); });
        waterBtn.onClick.AddListener(delegate { OnPick(Element.Water); });
        windBtn.onClick.AddListener(delegate { OnPick(Element.Wind); });
        earthBtn.onClick.AddListener(delegate { OnPick(Element.Earth); });
        confirmBtn.onClick.AddListener(OnConfirm);

        if (logScroll != null)
            logScroll.onValueChanged.AddListener(OnLogScrollChanged);

        // ▼これらは StartGame() に移動
        // _playerHp = maxHp;
        // _enemyHp = maxHp;
        // UpdateUI();
        // Log("バトル開始！");
        // NewTurn();
    }

    // マッチング成功時に呼ばれる
    public void StartGame()
    {
        // 何度呼ばれても同じ状態になるよう“毎回初期化”
        _playerHp = maxHp;
        _enemyHp = maxHp;
        _playerPicks.Clear();
        _enemyPicks.Clear();
        _playerStunned = _enemyStunned = false;
        _playerHasHaste = _enemyHasHaste = false;
        _turn = 1;

        // UI初期化
        if (playerPickText) playerPickText.text = "選択: -";
        if (_logBuffer != null) _logBuffer.Clear();
        UpdateUI();
        Log("バトル開始！");
        NewTurn();
    }

    void OnPick(Element e)
    {
        if (_playerStunned) return;
        if (_playerPicks.Count >= 2) return;
        _playerPicks.Add(e);
        RefreshPickText();
    }

    // OnDestroy() を追加（イベント解除）
    void OnDestroy()
    {
        if (logScroll != null)
            logScroll.onValueChanged.RemoveListener(OnLogScrollChanged);
    }

    public void RefreshPickText()
    {
        if (_playerPicks.Count == 0) playerPickText.text = "選択: -";
        else if (_playerPicks.Count == 1) playerPickText.text = "選択: " + _playerPicks[0].ToString();
        else playerPickText.text = "選択: " + _playerPicks[0].ToString() + " + " + _playerPicks[1].ToString();
    }

    void OnConfirm()
    {
        if (!_playerStunned && _playerPicks.Count < 2)
        {
            Log("属性を2つ選んでください。");
            return;
        }

        // ★オンラインモード：自分の選択を送って待機
        if (net != null && net.enabled)
        {
            net.SubmitMyPick(_playerPicks[0], _playerPicks[1]);
            Log("送信しました。相手の入力を待っています…");
            confirmBtn.interactable = false;
            return; // ← ここで終了（AIは動かさない）
        }

        // ▼以下はオフラインモード（AI戦）の処理
        // 敵の選択（とりあえず固定パターン＋乱数）
        _enemyPicks.Clear();
        if (_enemyStunned)
        {
            // 何もしない
        }
        else
        {
            int r = Random.Range(0, 3);
            if (r == 0) { _enemyPicks.Add(Element.Fire); _enemyPicks.Add(Element.Fire); }
            else if (r == 1) { _enemyPicks.Add(Element.Water); _enemyPicks.Add(Element.Wind); }
            else { _enemyPicks.Add(Element.Earth); _enemyPicks.Add(Element.Fire); }
        }

        // スペル化
        Spell playerSpell = null;
        Spell enemySpell = null;

        if (!_playerStunned) playerSpell = spellBook.Find(_playerPicks[0], _playerPicks[1]);
        if (!_enemyStunned && _enemyPicks.Count == 2) enemySpell = spellBook.Find(_enemyPicks[0], _enemyPicks[1]);

        bool playerFirst = DecideInitiative();

        ResolveTurn(playerFirst, playerSpell, enemySpell);

        if (CheckEnd()) return;

        NextTurnSetup();
        NewTurn();
    }

    bool DecideInitiative()
    {
        if (_playerHasHaste && !_enemyHasHaste) return true;
        if (_enemyHasHaste && !_playerHasHaste) return false;
        return (_turn % 2) == 1; // 交互先手
    }

    void ResolveTurn(bool playerFirst, Spell pSpell, Spell eSpell)
    {
        string psName = (pSpell != null) ? pSpell.DisplayNameJP : "行動不能/不発";
        string esName = (eSpell != null) ? eSpell.DisplayNameJP : "行動不能/不発";
        Log("あなた: " + psName + " / 敵: " + esName);

        _playerHasHaste = false;
        _enemyHasHaste = false;

        if (playerFirst)
        {
            ApplySpell(pSpell, true);
            ApplySpell(eSpell, false);
        }
        else
        {
            ApplySpell(eSpell, false);
            ApplySpell(pSpell, true);
        }
        UpdateUI();
    }

    void ApplySpell(Spell s, bool isPlayer)
    {
        if (s == null) return; // 行動不能 or 不発

        for (int i = 0; i < s.Effects.Count; i++)
        {
            var eff = s.Effects[i];
            switch (eff.Kind)
            {
                case EffectKind.Damage:
                    if (isPlayer)
                    {
                        _enemyHp = Mathf.Max(0, _enemyHp - eff.Value);
                        Log("あなたの" + s.DisplayNameJP + "！ 敵に" + eff.Value + "ダメージ");
                    }
                    else
                    {
                        _playerHp = Mathf.Max(0, _playerHp - eff.Value);
                        Log("敵の" + s.DisplayNameJP + "！ あなたに" + eff.Value + "ダメージ");
                    }
                    break;

                case EffectKind.Pierce:
                    if (isPlayer)
                    {
                        _enemyHp = Mathf.Max(0, _enemyHp - eff.Value);
                        Log("あなたの" + s.DisplayNameJP + "！（防御無視） 敵に" + eff.Value + "ダメージ");
                    }
                    else
                    {
                        _playerHp = Mathf.Max(0, _playerHp - eff.Value);
                        Log("敵の" + s.DisplayNameJP + "！（防御無視） あなたに" + eff.Value + "ダメージ");
                    }
                    break;

                case EffectKind.Heal:
                    if (isPlayer)
                    {
                        _playerHp = Mathf.Min(maxHp, _playerHp + eff.Value);
                        Log("あなたは" + eff.Value + "回復");
                    }
                    else
                    {
                        _enemyHp = Mathf.Min(maxHp, _enemyHp + eff.Value);
                        Log("敵は" + eff.Value + "回復");
                    }
                    break;

                case EffectKind.Stun:
                    if (isPlayer)
                    {
                        _enemyStunned = true;
                        Log("敵は" + eff.Duration + "ターン行動不能！");
                    }
                    else
                    {
                        _playerStunned = true;
                        Log("あなたは" + eff.Duration + "ターン行動不能！");
                    }
                    break;

                case EffectKind.Haste:
                    if (isPlayer)
                    {
                        _playerHasHaste = true;
                        Log("あなたは先手を取る体勢だ！");
                    }
                    else
                    {
                        _enemyHasHaste = true;
                        Log("敵は先手を取る体勢だ！");
                    }
                    break;
            }
        }
    }

    bool CheckEnd()
    {
        if (_playerHp <= 0 && _enemyHp <= 0)
        {
            Log("相打ち！引き分け");
            DisableInputs();
            return true;
        }
        if (_playerHp <= 0)
        {
            Log("あなたの敗北…");
            DisableInputs();
            return true;
        }
        if (_enemyHp <= 0)
        {
            Log("勝利！");
            DisableInputs();
            return true;
        }
        return false;
    }

    void DisableInputs()
    {
        fireBtn.interactable = false;
        waterBtn.interactable = false;
        windBtn.interactable = false;
        earthBtn.interactable = false;
        confirmBtn.interactable = false;
    }

    void NextTurnSetup()
    {
        // スタンは1回で解除
        _playerStunned = false;
        _enemyStunned = false;

        _playerPicks.Clear();
        _enemyPicks.Clear();

        _turn++;
    }

    void NewTurn()
    {
        RefreshPickText();
        Log("--- ターン " + _turn + " ---");
    }

    public void UpdateUI()
    {
        playerHpText.text = "HP: " + _playerHp + "/" + maxHp;
        enemyHpText.text = "HP: " + _enemyHp + "/" + maxHp;
    }

    public void Log(string msg)
    {
        _logBuffer.Add(msg);
        if (_logBuffer.Count > maxLogLines)
            _logBuffer.RemoveAt(0);

        logText.text = string.Join("\n", _logBuffer);

        // オートスクロールは「最下端にいるときだけ」、かつ「レイアウト確定後」に実行
        if (logScroll != null && _autoScroll)
        {
            StopCoroutine(nameof(CoScrollToBottomNextFrame));
            StartCoroutine(CoScrollToBottomNextFrame());
        }
    }
    IEnumerator CoScrollToBottomNextFrame()
    {
        // レイアウト更新を2回挟むと安定します
        Canvas.ForceUpdateCanvases();
        yield return null; // 1フレーム待つ（レイアウト反映）
        Canvas.ForceUpdateCanvases();

        logScroll.verticalNormalizedPosition = 0f; // 下端へ
        Canvas.ForceUpdateCanvases();
    }

    // スクロール位置監視（下端＝y≒0のときだけ自動スクロールON）
    void OnLogScrollChanged(Vector2 pos)
    {
        // 1=上端, 0=下端
        _autoScroll = (pos.y <= 0.001f);
    }
}
