using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ClassLibrary1
{
    public class Class1
    {
        public void DoesFileExist()
        {
            var searchTask = Windows.ApplicationModel.Package.Current.InstalledLocation.GetFileAsync("somepath").AsTask();
            var fileSearchTask = Windows.ApplicationModel.Package.Current.InstalledLocation.GetFilesAsync().AsTask();
        }
    }
}
