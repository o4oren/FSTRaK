using System;

namespace FSTRaK.Models
{
    internal class Aircraft : BaseModel
    {
        public string Title { get; set; }
        public String Manufacturer { get; set; }
        public String Code { get; set; }
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
    }
}
