using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace FSTRaK.Models
{
    internal class Aircraft : BaseModel
    {
        public int ID { get; set; }
        public string Title { get; set; } 
        
        public String AircraftType { get; set; }
    
        public String Airline { get; set; }
        public String Model { get; set; }
        public String TailNumber { get; set; }

        public override bool Equals(Object obj)
        {
            if ((obj == null) || !this.GetType().Equals(obj.GetType()))
            {
                return false;
            }
            else
            {
                return ((Aircraft)obj).Title == this.Title;
            }
        }

        public override int GetHashCode()
        {
            int hashCode = -702049157;
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Title);
            hashCode = hashCode * -1521134295 + EqualityComparer<string>.Default.GetHashCode(Model);
            return hashCode;
        }
    }
}
