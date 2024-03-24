using System.Collections.Generic;
using System.Globalization;
using AirPlaneTicketManagement.Utils;

namespace AirPlaneTicketManagement.Data;

public class Passenger
{
    public int Id { get;  set; }
    public string FullName { get;  set; } 
    public PassengerType Type { get;  set; }
    public int Age { get; private set; }
    public int? FamilyId { get;  set; } 

    public List<Seat> Seats = new List<Seat>();
   

    public Passenger(int id, string fullName, PassengerType type, int age, int? familyId)
    {
        Id = id;
        FullName = fullName;
        Type = type;
        Age = age;
        FamilyId = familyId;
    }

    public void AddSeat(Seat seat)
    {
        Seats.Add(seat);
    }

    public void RemoveSeat(Seat seat)
    {
        Seats.Remove(seat);
    }

    public double GetTicketPrice()
    {
        return Type switch
        {
            PassengerType.Adult => 250,
            PassengerType.Child => 150,
            PassengerType.AdultRequiringTwoSeats => 500,
            _ => throw new InvalidOperationException("Invalid passenger type.")
        };
    }

    public override string ToString()
    {
        var seatRequirement = Type == PassengerType.AdultRequiringTwoSeats ? "Requires two seats" : "Requires one seat";
        return $"{FullName} ({Type}) - {Age} years - Family: {FamilyId?.ToString() ?? "N/A"} - {seatRequirement}";
    }
}