using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Data.SQLite;
using System.Data.SQLite.EF6;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace FSTRaK.Models
{
    internal class SQLiteConfiguration : DbConfiguration
    {
        public SQLiteConfiguration()
        {
            SetProviderFactory("System.Data.SQLite", SQLiteFactory.Instance);
            SetProviderServices("System.Data.SQLite", SQLiteProviderFactory.Instance.GetService(typeof(System.Data.Entity.Core.Common.DbProviderServices)) as System.Data.Entity.Core.Common.DbProviderServices);
        }
    }
}
