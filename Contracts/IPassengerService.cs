using AirPlaneTicketManagement.Data;

namespace AirPlaneTicketManagement.Contracts;

public interface IPassengerService
{
    public (List<Passenger> passengers, List<Family> families) GeneratePassengersAndFamilies();
    public void DisplayPassengersAndFamilies(List<Passenger> passengers, List<Family> families);
}
