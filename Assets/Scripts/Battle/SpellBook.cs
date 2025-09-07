using System.Collections.Generic;
using UnityEngine;

public class SpellBook : MonoBehaviour
{
    [Tooltip("使用可能なSpellアセットを登録（Resources/Spells 推奨）")]
    public List<Spell> Recipes = new List<Spell>();

    private Dictionary<System.ValueTuple<Element, Element>, Spell> _map =
        new Dictionary<System.ValueTuple<Element, Element>, Spell>();

    void Awake()
    {
        BuildMap();
    }

    public void BuildMap()
    {
        _map.Clear();
        for (int i = 0; i < Recipes.Count; i++)
        {
            var s = Recipes[i];
            if (s == null) continue;
            var key = s.NormalizedKey();
            _map[key] = s;
        }
    }

    public Spell Find(Element a, Element b)
    {
        System.ValueTuple<Element, Element> key;
        if ((int)a <= (int)b) key = new System.ValueTuple<Element, Element>(a, b);
        else key = new System.ValueTuple<Element, Element>(b, a);

        Spell s;
        if (_map.TryGetValue(key, out s)) return s;
        return null; // 未定義なら不発
    }
}
