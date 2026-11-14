using System;
using System.Collections.Generic;
using System.Text;

namespace SoftwareFactory.Core.Projects
{
    public class Project
    {
        public string Id { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Goal { get; set; } = string.Empty;
        public string Path { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public void LoadProject(string projectPath)
        {
            // Load project details from a file or database
            // For example, you can read a JSON file containing project information
            // and populate the properties of the Project class.
        }

    }

}
