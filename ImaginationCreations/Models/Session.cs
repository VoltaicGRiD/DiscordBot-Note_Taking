using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImaginationCreations.Models
{
    public class Session
    {
        public int Id { get; set; }
        public DateTime Created { get; set; }
        public string Name { get; set; }
        public string Game { get; set; }
        public string GameName { get; set; }
        public string PlayersRaw { get; set; }
        public List<ulong> Players 
        { 
            get
            {
                List<ulong> list = new List<ulong>();

                foreach (var p in PlayersRaw.Split(';'))
                {
                    list.Add(ulong.Parse(p));
                }

                return list;
            }
        }
        public bool Ended { get; set; }
    }
}
