using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ImaginationCreations.Models
{
    public class Journal
    {
        public int Id { get; set; }
        public DateTime Created { get; set; }
        public string User { get; set; }
        public string Title { get; set; }
        public string Content { get; set; }
    }
}
