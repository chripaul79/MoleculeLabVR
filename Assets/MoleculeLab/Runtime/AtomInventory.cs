using System;
using System.Collections.Generic;
using System.Linq;
namespace MoleculeLab
{
    /// <summary>Each atom has one identity and exactly one owner throughout a session.</summary>
    public sealed class AtomInventory
    {
        public const string Shelf = "shelf";
        public sealed class Token
        {
            public int Id { get; internal set; }
            public string Symbol { get; internal set; }
            public string Owner { get; internal set; }
        }
        readonly List<Token> tokens = new List<Token>();
        public IReadOnlyList<Token> Tokens => tokens;
        public AtomInventory()
        {
            foreach(var entry in new[]{("C",8),("O",8),("H",32)})
                for(var i=0;i<entry.Item2;i++) tokens.Add(new Token { Id=tokens.Count,Symbol=entry.Item1,Owner=Shelf });
        }
        public bool Transfer(int id,string expected,string next)
        {
            if(id<0 || id>=tokens.Count || string.IsNullOrEmpty(next) || tokens[id].Owner!=expected) return false;
            tokens[id].Owner=next; return true;
        }
        public int Available(string symbol) => tokens.Count(t=>t.Symbol==symbol && t.Owner==Shelf);
        public int FirstAvailable(string symbol) => tokens.FindIndex(t=>t.Symbol==symbol && t.Owner==Shelf);
        public void ReturnOwner(string owner) { foreach(var token in tokens.Where(t=>t.Owner==owner)) token.Owner=Shelf; }
    }
}
