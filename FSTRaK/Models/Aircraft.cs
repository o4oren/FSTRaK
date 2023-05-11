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

        public string Details
        {
            get
            {
                return $"Lat: {Position[0]:F4} Lon:{Position[1]:F4} \nHeading: {Heading:F0} Alt: {Altitude:F0} ft\nSpeed: {Airspeed:F0} Knots";
            }
        }

        private double heading;
        public double Heading
        {
            get => heading;
            set
            {
                if (value != heading)
                {
                    heading = value;
                    OnPropertyChanged();
                }
            }
        }
        private double altitude;
        public double Altitude
        {
            get => altitude;
            set
            {
                if (value != altitude)
                {
                    altitude = value;
                    OnPropertyChanged();
                }
            }
        }
        private double airspeed;
        public double Airspeed
        {
            get => airspeed;
            set
            {
                if (value != airspeed)
                {
                    airspeed = value;
                    OnPropertyChanged();
                }
            }
        }

        private double[] position = new double[2];
        public double[] Position
        {
            get => position;
            set
            {
                position = value;
                OnPropertyChanged(string.Empty);
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
            hashCode = hashCode * -1521134295 + Heading.GetHashCode();
            hashCode = hashCode * -1521134295 + Altitude.GetHashCode();
            hashCode = hashCode * -1521134295 + Airspeed.GetHashCode();
            hashCode = hashCode * -1521134295 + EqualityComparer<double[]>.Default.GetHashCode(Position);
            return hashCode;
        }
    }
}
