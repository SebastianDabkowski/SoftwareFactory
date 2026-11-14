using System;
using System.Collections.Generic;
using System.Text;

namespace SoftwareFactory.Core.Projects
{
    public class UserComments
    {
        public string Id { get; set; } = string.Empty;
        public string Comment { get; set; } = string.Empty;
        public DateTime Timestamp { get; set; }
        public string ProjectId { get; set; } = string.Empty;
    }
}
