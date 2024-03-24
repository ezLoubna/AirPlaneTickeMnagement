using AirPlaneTicketManagement.Contracts;
using AirPlaneTicketManagement.Data;
using AirPlaneTicketManagement.Utils;


namespace AirPlaneTicketManagement.Services;

public class PassengerService : IPassengerService
{
    public (List<Passenger> passengers, List<Family> families) GeneratePassengersAndFamilies()
    {
        var passengers = new List<Passenger>();
        var families = new List<Family>();
        var random = new Random();
        int passengerIdCounter = 1;
        int familyIdCounter = 1;
        int maxPassengers = 200;

        while (maxPassengers > 0)
        {
            bool isFamily = random.Next(0, 2) == 1; // Randomly decide whether to generate a family or an individual adult
            if (isFamily)
            {
                var family = GenerateFamily(ref passengerIdCounter, familyIdCounter++, random);
                int familySeatsRequired = family.GetTotalSeatsRequired();
                if (maxPassengers >= familySeatsRequired)
                {
                    families.Add(family);
                    maxPassengers -= familySeatsRequired;
                }
            }
            else
            {
                var adult = GenerateAdult(ref passengerIdCounter, null, random);
                int adultSeatsRequired = adult.Type == PassengerType.AdultRequiringTwoSeats ? 2 : 1;
                if (maxPassengers >= adultSeatsRequired)
                {
                    passengers.Add(adult);
                    maxPassengers -= adultSeatsRequired;
                }
            }
        }

        return (passengers, families);
    }
    private Family GenerateFamily(ref int passengerIdCounter, int familyId, Random random)
    {
        var family = new Family(familyId);

        int numChildren = random.Next(1, 4);
        for (int i = 0; i < numChildren; i++)
        {
            var child = new Passenger(passengerIdCounter++, "Child", PassengerType.Child, random.Next(0, 13), family.Id);
            family.Members.Add(child);
        }
        //Deciding the number of adults based on the number of childre 
        int numAdults = numChildren == 3 ? 2 : random.Next(1, 3);
        for (int i = 0; i < numAdults; i++)
        {
            var adult = GenerateAdult(ref passengerIdCounter, family.Id, random);
            family.Members.Add(adult);
        }

        return family;
    }
    private Passenger GenerateAdult(ref int passengerIdCounter, int? familyId, Random random)
    {
        var possibleAdultTypes = new[] { PassengerType.Adult, PassengerType.AdultRequiringTwoSeats };
        var randomType = possibleAdultTypes[random.Next(possibleAdultTypes.Length)];
        return new Passenger(passengerIdCounter++, "Adult", randomType, random.Next(12, 71), familyId);
    }
    public void DisplayPassengersAndFamilies(List<Passenger> passengers, List<Family> families)
    {
        Console.WriteLine("List of passengers and families:\n");
        int totalSeatsRequired = 0;
        double totalPrice = 0;

        foreach (var family in families)
        {
            int familySeats = family.GetTotalSeatsRequired();
            double familyPrice = family.GetTotalPrice();
            Console.WriteLine($"Family {family.Id} - Total Seats: {familySeats}, Total Price: {familyPrice:0.00}€");

            foreach (var member in family.Members)
            {
                Console.WriteLine($"- {member.FullName} - Id: {member.Id} - ({member.Type} - {member.Age} years old)");
            }

            totalSeatsRequired += familySeats;
            totalPrice += familyPrice;
            Console.WriteLine();
        }

        foreach (var passenger in passengers.Where(p => p.FamilyId == null))
        {
            Console.WriteLine($"{passenger.FullName} - Id: {passenger.Id} - ({passenger.Type} - {passenger.Age} years old) is traveling alone.");
            totalSeatsRequired += passenger.Type == PassengerType.AdultRequiringTwoSeats ? 2 : 1;
            totalPrice += passenger.GetTicketPrice();
        }

        Console.WriteLine($"Total Required Seats: {totalSeatsRequired}");
        Console.WriteLine($"Total Tickets Price: {totalPrice:0.00}€");
    }
}

