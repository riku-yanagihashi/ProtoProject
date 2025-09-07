using System.Collections.Generic;
using UnityEngine;

[CreateAssetMenu(menuName = "MagicBattle/Spell", fileName = "NewSpell")]
public class Spell : ScriptableObject
{
    [Header("識別子/表示名")]
    public string Id;            // 例: "F+F"
    public string DisplayNameJP; // 例: 大火球

    [Header("レシピ（元素2つ）")]
    public Element A;
    public Element B;

    [Header("効果一覧（上から順に適用）")]
    public List<Effect> Effects = new List<Effect>();

    public System.ValueTuple<Element, Element> NormalizedKey()
    {
        // A,Bの順に依存しないよう小さい方→大きい方に揃える
        if ((int)A <= (int)B) return new System.ValueTuple<Element, Element>(A, B);
        return new System.ValueTuple<Element, Element>(B, A);
    }
}
