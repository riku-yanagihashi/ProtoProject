using System;

public enum EffectKind
{
    Damage,   // 通常ダメージ
    Heal,     // 回復
    Stun,     // 行動不能（次ターン）
    Pierce,   // 防御無視ダメージ（将来の防御導入を見越して）
    Haste     // 先手獲得（このターンの解決で先に処理）
    // 追加候補：Shield, Amplify, Weaken, Dot, Hot など
}

[Serializable]
public struct Effect
{
    public EffectKind Kind;
    public int Value;     // Damage/Heal/Pierce など
    public int Duration;  // Stun/Haste などの持続（本プロトは 1 推奨）
}
