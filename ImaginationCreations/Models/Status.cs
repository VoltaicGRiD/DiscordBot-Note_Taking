using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImaginationCreations.Models
{
    public class Status
    {
        public int SessionId { get; set; }
        public bool SessionEnded { get; set; }
        public int NotesThisSession { get; set; }
        public int NotesAllTime { get; set; }
        public Note LastNote { get; set; }
    }
}
