using FSTRaK.DataTypes;
using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text;

namespace FSTRaK.Models
{
    public class Aircraft : BaseModel
    {
        [Column("ID")]
        public int Id { get; set; }

        [Index(nameof(Title), IsUnique = true)]
        public string Title { get; set; } 
        public String AircraftType { get; set; }

        public String Category { get; set; }
        public String Manufacturer { get; set; }
        public String Airline { get; set; }
        public String Model { get; set; }
        public String TailNumber { get; set; }
        public int NumberOfEngines { get; set; }
        public EngineType EngineType { get; set; }

        public double? EmptyWeightLbs { get; set; }
        public override bool Equals(Object obj)
        {
            if ((obj == null) || this.GetType() != obj.GetType())
            {
                return false;
            }
            else
            {
                return ((Aircraft)obj).Title == this.Title;
            }
        }

        public override string ToString()
        {
            var builder = new StringBuilder();
            builder.AppendLine(this.Title)
            .AppendLine($"{this.Manufacturer} {this.AircraftType}")
            .AppendLine(this.TailNumber);
            if (this.Airline != string.Empty)
                builder.Append(this.Airline);
            return builder.ToString();
        }

        public override int GetHashCode()
        {
            var hashCode = -702049157;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Title);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Model);
            return hashCode;
        }
    }
}
