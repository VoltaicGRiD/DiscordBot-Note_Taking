using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImaginationCreations.Models
{
    public class Note
    {
        public int Id { get; set; }
        public string User { get; set; }
        public int SessionId { get; set; }
        private KeyValuePair<string, string> Names {
            get
            {
                return SqlHelper.GetNames(SessionId);
            }
        }
        public string Session 
        { 
            get
            {
                return Names.Value;
            } 
        }
        public string Game
        {
            get
            {
                return Names.Key;
            }
        }
        public DateTime Created { get; set; }
        public string Content { get; set; }

        public bool Voided { get; set; }
    }
}
