using FSTRaK.BusinessLogic.VatsimService.VatsimModel;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.Utils
{
    public class TimeUtils
    {
        public static string GetConnectionsSinceFromTimeString(string time)
        {
            DateTime specifiedTime = DateTime.Parse(time, null, System.Globalization.DateTimeStyles.RoundtripKind);
            DateTime currentTime = DateTime.UtcNow;
            TimeSpan timeDifference = currentTime - specifiedTime;
            return $"{timeDifference.Hours:0#}:{timeDifference.Minutes:0#}:{timeDifference.Seconds:0#}";
        }

    }
}
