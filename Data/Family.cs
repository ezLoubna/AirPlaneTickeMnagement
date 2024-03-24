using System.Text;
using AirPlaneTicketManagement.Utils;

namespace AirPlaneTicketManagement.Data;

public class Family
{
    public int Id { get; private set; }
    public List<Passenger> Members = new List<Passenger>();
    public List<Seat>? AllocatedSeats = new List<Seat>();
    
    public Family(int id)
    {
        Id = id;
    }

    public void AddMember(Passenger member)
    {
        if (member == null) return;

        member.FamilyId = Id;
        Members.Add(member);
    }

    public bool RemoveMember(Passenger member)
    {
        return Members.Remove(member);
    }

    public double GetTotalPrice()
    {
        return Members.Sum(member => member.GetTicketPrice());
    }

    public override string ToString()
    {
        var sb = new StringBuilder($"Family ID: {Id} - Total Members: {Members.Count}\n");

        foreach (var member in Members)
        {
            sb.AppendLine($"- {member}");
        }

        sb.AppendLine($"Total Price: {GetTotalPrice()} €");
        return sb.ToString();
    }

    public int GetTotalSeatsRequired()
    {
        return Members.Sum(member => member.Type == PassengerType.AdultRequiringTwoSeats ? 2 : 1);
    }

    public bool HasSeatsAssigned()
    {
        return Members.Any(m => m.Seats != null && m.Seats.Count > 0);
    }

    public int GetAdultCount()
    {
        return Members.Count(m => m.Type == PassengerType.Adult || m.Type == PassengerType.AdultRequiringTwoSeats);
    }

    public int GetChildrenCount()
    {
        return Members.Count(m => m.Type == PassengerType.Child);
    }

    public int GetAdultTwoSeatsCount()
    {
        return Members.Count(m => m.Type == PassengerType.AdultRequiringTwoSeats);
    }

   
    public double GetAverageAge()
    {
        if (Members.Count == 0) return 0;
        return Members.Average(m => m.Age);
    }

    public bool AllMembersHaveSeats()
    {
        return Members.All(m => m.Seats != null && m.Seats.Any());
    }
}
