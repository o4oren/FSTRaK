using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.Utils
{
    internal class PathUtil
    {
        public static string GetApplicationLocalDataPath()
        {
            var localPath = Path.Combine(Environment.GetFolderPath(System.Environment.SpecialFolder.LocalApplicationData), System.Reflection.Assembly.GetExecutingAssembly().GetName().Name);
#if DEBUG
            localPath += "_DEBUG";
#endif
            return localPath;
        }
    }
}
