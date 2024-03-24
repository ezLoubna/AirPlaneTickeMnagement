using AirPlaneTicketManagement.Data;

namespace AirPlaneTicketManagement.Contracts;

public interface ISeatAssignementService
{
    public  (List<Passenger> passengers, List<Family> families) AssignSeats(List<Passenger> passengers, List<Family> families);
    public void DisplaySeatAssignment(List<Passenger> Passengers, List<Family> Families);
}
