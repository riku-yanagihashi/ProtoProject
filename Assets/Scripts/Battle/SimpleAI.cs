using System.Collections.Generic;
using UnityEngine;
//using MagicBattle.Data; // ← Element を使うために必要

public class SimpleAI : MonoBehaviour
{
    [Header("HP が閾値以下なら回復を優先")]
    public int lowHpThreshold = 8;

    [Header("行動バイアス（0〜1）")]
    [Range(0f, 1f)] public float healBiasLowHp = 0.7f; // 低HP時に Water+Water を選ぶ確率
    [Range(0f, 1f)] public float burstBias = 0.4f; // Fire+Fire（火力）を選ぶ確率
    [Range(0f, 1f)] public float controlBias = 0.2f; // Water+Wind（スタン狙い）を選ぶ確率
    // 残りは Earth+Fire（防御無視）を選択

    /// <summary>
    /// 現在の（敵＝AI側の）HPから、そのターンの元素2つを選びます。
    /// </summary>
    public List<Element> Choose(int currentHp)
    {
        var picks = new List<Element>(2);

        // 低HPなら回復（Water+Water）を優先
        if (currentHp <= lowHpThreshold)
        {
            if (Random.value < healBiasLowHp)
            {
                picks.Add(Element.Water);
                picks.Add(Element.Water);
                return picks;
            }
        }

        // 通常時は確率で行動を分岐
        float r = Random.value;
        if (r < burstBias)
        {
            // 火力重視
            picks.Add(Element.Fire);
            picks.Add(Element.Fire);
        }
        else if (r < burstBias + controlBias)
        {
            // 行動妨害（スタン）
            picks.Add(Element.Water);
            picks.Add(Element.Wind);
        }
        else
        {
            // 防御無視ダメージ
            picks.Add(Element.Earth);
            picks.Add(Element.Fire);
        }

        return picks;
    }
}
