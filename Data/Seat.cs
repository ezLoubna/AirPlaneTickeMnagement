using AirPlaneTicketManagement.Data;

public class Seat
{
    public int Row { get;  set; }
    public int Column { get;  set; }
    public bool IsOccupied { get;  set; }
    public Passenger? Occupant { get;  set; }
    public string Label { get;  set; }
  

    public Seat(int row, int column)
    {
        Row = row;
        Column = column;
        Label = $"{row}{(char)('A' + column)}";     
        IsOccupied = false;
    }

    public bool AssignTo(Passenger passenger)
    {
        if (IsOccupied) return false;

        Occupant = passenger;
        IsOccupied = true;
        return true;
    }

    public bool IsAssignedTo(Family family)
    {
        return Occupant != null && family.Members.Contains(Occupant);
    }

    public bool IsAdjacentTo(Seat otherSeat)
    {
        return Row == otherSeat.Row && Math.Abs(Column - otherSeat.Column) == 1;
    }
  
   
}