using System;
using System.Collections.Generic;
using System.Linq;
namespace MoleculeLab
{
    /// <summary>
    /// Each atom has one identity and exactly one owner throughout a session. Every basket socket starts with one atom.
    /// Finite mode: atoms leave and return to their own socket. Unlimited mode: taking an atom immediately puts a fresh
    /// one in its socket; an atom handed back to a full socket is retired (removed from play).
    /// </summary>
    public sealed class AtomInventory
    {
        public const string Shelf = "shelf";
        public const string Retired = "retired";
        public sealed class Token
        {
            public int Id { get; internal set; }
            public string Symbol { get; internal set; }
            public string Owner { get; internal set; }
            /// <summary>The basket socket this atom belongs to.</summary>
            public int Socket { get; internal set; }
        }
        public static readonly string[] DefaultLayout=Enumerable.Repeat("C",8).Concat(Enumerable.Repeat("O",8)).Concat(Enumerable.Repeat("H",32)).ToArray();
        readonly List<Token> tokens = new List<Token>();
        readonly string[] sockets;
        public IReadOnlyList<Token> Tokens => tokens;
        public int SocketCount => sockets.Length;
        public string SocketSymbol(int socket) => sockets[socket];
        /// <summary>When true, baskets never run out.</summary>
        public bool Unlimited { get; set; }
        /// <summary>Raised with the new token id when unlimited mode refills a socket.</summary>
        public event Action<int> Refilled;
        public AtomInventory() : this(DefaultLayout) { }
        public AtomInventory(IEnumerable<string> socketSymbols,bool unlimited=false)
        {
            sockets=socketSymbols.ToArray(); Unlimited=unlimited;
            for(var socket=0;socket<sockets.Length;socket++) tokens.Add(new Token { Id=tokens.Count,Symbol=sockets[socket],Owner=Shelf,Socket=socket });
        }
        public bool Transfer(int id,string expected,string next)
        {
            if(id<0 || id>=tokens.Count || string.IsNullOrEmpty(next) || next==Retired || tokens[id].Owner!=expected || expected==Retired) return false;
            var token=tokens[id]; token.Owner=next;
            if(expected==Shelf && next!=Shelf && Unlimited)
            {
                var fresh=new Token { Id=tokens.Count,Symbol=token.Symbol,Owner=Shelf,Socket=token.Socket };
                tokens.Add(fresh); Refilled?.Invoke(fresh.Id);
            }
            if(next==Shelf && expected!=Shelf && tokens.Any(t=>t!=token && t.Owner==Shelf && t.Socket==token.Socket)) token.Owner=Retired;
            return true;
        }
        public bool IsRetired(int id) => id>=0 && id<tokens.Count && tokens[id].Owner==Retired;
        public int Available(string symbol) => tokens.Count(t=>t.Symbol==symbol && t.Owner==Shelf);
        public int FirstAvailable(string symbol) => tokens.FindIndex(t=>t.Symbol==symbol && t.Owner==Shelf);
        /// <summary>Tokens still in play (on the shelf, held, or in a molecule).</summary>
        public IEnumerable<Token> Live => tokens.Where(t=>t.Owner!=Retired);
        public void ReturnOwner(string owner) { foreach(var token in tokens.Where(t=>t.Owner==owner).ToArray()) Transfer(token.Id,owner,Shelf); }
    }
}
