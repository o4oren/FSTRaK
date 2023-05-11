using System;
using System.Collections.Generic;

namespace FSTRaK.Models
{
    internal class Aircraft : BaseModel
    {
        private string title;

        public string Title { get => title; set 
            {
                if(value != title)
                {
                    title = value;
                    OnPropertyChanged();
                }
            } 
        }
        private string type;
        public String Type { get => title; 
            set
            {
                if (value != type)
                {
                    type = value;
                    OnPropertyChanged();
                }
            }
        }

        private string airline;
        public String Airline
        {
            get => title;
            set
            {
                if (value != airline)
                {
                    airline = value;
                    OnPropertyChanged();
                }
            }
        }
        private string model;
        public String Model
        {
            get => model;
            set
            {
                if (value != model)
                {
                    model = value;
                    OnPropertyChanged();
                }
            }
        }
        private string tailNumber;
        public String TailNumber
        {
            get => tailNumber;
            set
            {
                if (value != tailNumber)
                {
                    tailNumber = value;
                    OnPropertyChanged();
                }
            }
        }
        

 

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
