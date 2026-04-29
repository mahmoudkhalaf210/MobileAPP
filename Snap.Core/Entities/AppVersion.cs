using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace Snap.Core.Entities
{
    public class AppVersion
    {
        public int Id { get; set; }
        public string Platform { get; set; } // android / ios
        public string LatestVersion { get; set; }
        public string MinSupportedVersion { get; set; }
        public string UpdateUrl { get; set; }
        public string Message { get; set; }
    }
}
