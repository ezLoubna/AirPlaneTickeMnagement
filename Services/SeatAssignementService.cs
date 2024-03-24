using AirPlaneTicketManagement.Contracts;
using AirPlaneTicketManagement.Data;
using AirPlaneTicketManagement.Utils;
using System.Collections.Generic;
using System.Text;

namespace AirPlaneTicketManagement.Services;

public class SeatAssignementService : ISeatAssignementService
{

    private const int NumRows = 34; // 33 rangées de 6 sièges et 1 rangée de 2 sièges
    private const int NumColumns = 6;

    // <summary>
    /// Assigns seats to passengers and families with the goal of seating families together whenever possible.
    /// This method first sorts passengers and families based on their seat requirements, giving priority
    /// to those needing more seats or with specific seating needs (e.g., adults requiring two seats).
    /// It then initializes the seating plan and dynamically tracks the remaining available seats.
    /// Families are allocated seats first to ensure they can be seated together, followed by individual passengers.
    /// If a family or passenger cannot be seated due to insufficient available seats, they are skipped.
    /// </summary>
    public (List<Passenger> passengers, List<Family> families) AssignSeats(List<Passenger> passengers, List<Family> families)
    {
        // Sort passengers and families by their seat requirements      
        var SortedPassengers = passengers.OrderByDescending(p => p.Type == PassengerType.AdultRequiringTwoSeats ? 2 : 1).ToList();
        var SortedFamilies = families.OrderByDescending(f => f.GetAdultTwoSeatsCount())
                                      .ThenByDescending(f => f.GetChildrenCount())
                                      .ToList();
        // Initialize the seats
        var seats = InitializeSeats();
        // Dynamically calculate remaining seats based on occupancy
        int remainingSeats = seats.Count(s => !s.IsOccupied);
        // Assign seats to families first to ensure they can sit together
        foreach (var Family in SortedFamilies)
        {
            if (Family.HasSeatsAssigned() || remainingSeats <= 0) continue;
            List<Seat>? AllocatedSeats = FindAvailableSeatsForFamily(Family, seats,ref remainingSeats);
            Family.AllocatedSeats = AllocatedSeats;
        }
        // Assign seats to individual passengers
        foreach (var passenger in SortedPassengers)
        {
            if (passenger.Seats != null && passenger.Seats.Count > 0 && remainingSeats<= 0) continue;
            List<Seat>? allocatedSeats =FindSeatForPassenger(passenger, seats,ref remainingSeats);
            passenger.Seats = allocatedSeats;
            
        }
        return (passengers, families);
    }

    public List<Seat> InitializeSeats()
    {
        List<Seat> seats = new List<Seat>();

        for (int row = 1; row <= NumRows; row++)
        {
            // Déterminez le nombre de sièges dans la rangée actuelle
            int seatsInRow = (row == NumRows) ? 2 : NumColumns;

            for (int col = 0; col < seatsInRow; col++)
            {
                seats.Add(new Seat(row, col));
            }
        }

        return seats;
    }
    /// <summary>
    /// Attempts to find available seats for a given family within the aircraft, either in a single row or across consecutive rows.
    /// The method first checks each row individually to see if it can accommodate the entire family.
    /// If not possible within a single row, it then checks for the possibility of splitting the family across two consecutive rows,
    /// starting from the second row and considering the previous row as well. This allows for a flexible seating arrangement
    /// that can accommodate various family sizes and seat requirements.
    /// It updates the remaining seats count as seats are allocated to ensure the seating capacity is not exceeded.
    /// </summary>
    private List<Seat>? FindAvailableSeatsForFamily(Family family, List<Seat> allSeats, ref int remainigSeats)
    {
        for (int row = 1; row <= NumRows; row++)
        {
            var rowSeats = allSeats.Where(s => s.Row == row && !s.IsOccupied).OrderBy(s => s.Column).ToList();
            if (rowSeats.Count > family.GetTotalSeatsRequired())
            {
                if (CanFamilyFitInSingleRow(family, rowSeats))
                {
                    List<Seat> allocatedSeats = AllocateFamilySeatsInRow(family, rowSeats);
                    if (allocatedSeats != null && allocatedSeats.Count > 0)
                    {
                        remainigSeats -= allocatedSeats.Count;
                        return allocatedSeats;
                    }
                }
            }
        }

        for (int row = 1; row < NumRows; row++)
        {
            var currentRowSeats = allSeats.Where(s => s.Row == row && !s.IsOccupied).OrderBy(s => s.Column).ToList();
            if (row > 1) // Starting from the second row, consider the previous row as well.
            {
                var previousRowSeats = allSeats.Where(s => s.Row == row - 1 && !s.IsOccupied).OrderBy(s => s.Column).ToList();
                if (CanFamilyBeSplitAcrossRows(family, previousRowSeats, currentRowSeats))
                {
                    List<Seat> allocatedSeats = AllocateFamilyMembersAcrossRows(family, previousRowSeats, currentRowSeats);
                    if (allocatedSeats != null && allocatedSeats.Count > 0)
                    {
                        remainigSeats -= allocatedSeats.Count;
                        return allocatedSeats;
                    }
                }
            }
            if (row < NumRows) // If not the last row, consider the next row as well.
            {
                var nextRowSeats = allSeats.Where(s => s.Row == row + 1 && !s.IsOccupied).OrderBy(s => s.Column).ToList();
                if (CanFamilyBeSplitAcrossRows(family, currentRowSeats, nextRowSeats))
                {
                    List<Seat> allocatedSeats = AllocateFamilyMembersAcrossRows(family, currentRowSeats, nextRowSeats);
                    if (allocatedSeats != null && allocatedSeats.Count > 0)
                    {
                        remainigSeats -= allocatedSeats.Count;
                        return allocatedSeats;
                    }
                }
            }           
        }

        return null;
    }
    /// <summary>
    /// Allocates seats to a family across two consecutive rows, ensuring that each child is seated next to an adult family member.
    /// The method first attempts to allocate seats based on the number of children in the family:
    /// - For one child, it tries to place the child and an adult together in one of the rows.
    /// - For two children, it explores options to place each child next to an adult or to place both children with one adult between them.
    /// - For three children, it aims to place two children with an adult in the middle in one row, and the remaining child and adult in the other.
    /// The allocation strategy prioritizes keeping family members together and fulfilling the requirement for adults to supervise the children.
    /// </summary>
    /// <param name="Family">The family requiring seat allocation.</param>
    /// <param name="CurrentRowSeats">List of available seats in the current row being considered.</param>
    /// <param name="NextRowSeats">List of available seats in the next consecutive row.</param>
    /// <returns>A list of seats allocated to the family members across the rows.</returns>
    public List<Seat> AllocateFamilyMembersAcrossRows(Family Family, List<Seat> CurrentRowSeats, List<Seat> NextRowSeats)
    {
        List<Seat> AllocatedSeats = new List<Seat>(); // Pour stocker les sièges alloués
        List<Passenger> Children = Family.Members.Where(m => m.Type == PassengerType.Child).ToList();
        List<Passenger> Adults = Family.Members.Where(m => m.Type == PassengerType.Adult || m.Type == PassengerType.AdultRequiringTwoSeats).ToList();
        // Commencez l'allocation basée sur le nombre d'enfants
        if (Children.Count() == 1)
        {
            AllocatedSeats.AddRange(AllocateChildAndAdult(Children[0], Adults, CurrentRowSeats, NextRowSeats));
        }
        else if (Children.Count() == 2)
        {
            AllocatedSeats.AddRange(AllocateTwoChildren(Children, Adults, CurrentRowSeats, NextRowSeats));
        }
        else if (Children.Count() == 3)
        {
            AllocatedSeats.AddRange(AllocateThreeChildren(Children, Adults, CurrentRowSeats, NextRowSeats));
        }

        return AllocatedSeats; // Retournez la liste des sièges alloués.
    }
    // <summary>
    /// Allocates seats for a family with three children, attempting to seat the family across two consecutive rows. 
    /// This method prioritizes placing an adult between two children (EAE configuration) where possible. 
    /// It attempts to allocate seats for an adult requiring two seats in this configuration first, 
    /// followed by an attempt with an adult requiring one seat. If EAE cannot be achieved in one row, 
    /// the next row is tried. If EAE configuration is not feasible, or once it is successfully achieved, 
    /// it proceeds to allocate the remaining family members ensuring each child is next to an adult. 
    /// Returns the final list of allocated seats, or null if allocation fails to meet family requirements.
    /// </summary>
    /// <param name="family">The family needing seat allocation.</param>
    /// <param name="currentRowSeats">List of seats in the current row.</param>
    /// <param name="nextRowSeats">List of seats in the consecutive next row.</param>
    /// <returns>List of allocated seats if successful; otherwise, null.</returns>
    private List<Seat> AllocateThreeChildren(List<Passenger> children, List<Passenger> adults, List<Seat> currentRowSeats, List<Seat> nextRowSeats)
    {
        List<Seat> allocatedSeats = new List<Seat>();

        var adultRequiringTwoSeats = adults.FirstOrDefault(a => a.Type == PassengerType.AdultRequiringTwoSeats);
        var adultRequiringOneSeat = adults.FirstOrDefault(a => a.Type == PassengerType.Adult);
        bool isAllocated= false;

        if (adultRequiringTwoSeats != null)
        {
            isAllocated = TryAllocateAdultBetweenChildren(children, adultRequiringTwoSeats, currentRowSeats);
        }
       
        if (!isAllocated && adultRequiringOneSeat!=null)
        {
            isAllocated = TryAllocateAdultBetweenChildren(children,adultRequiringOneSeat,currentRowSeats);
        }

        if (!isAllocated && adultRequiringTwoSeats != null)
        {
            isAllocated = TryAllocateAdultBetweenChildren(children, adultRequiringTwoSeats, nextRowSeats);
        }
        if (!isAllocated && adultRequiringOneSeat != null)
        {
            isAllocated = TryAllocateAdultBetweenChildren(children, adultRequiringOneSeat, nextRowSeats);
        }
       
        if (isAllocated)
        {            
            allocatedSeats.AddRange(children.SelectMany(c => c.Seats));
            allocatedSeats.AddRange(adults.SelectMany(c => c.Seats));           
            Passenger? remainingChild = children.FirstOrDefault(child => child.Seats.Count == 0);
            Passenger? remainingAdult = adults.FirstOrDefault(adult => adult.Seats.Count == 0);
            List<Seat> remainingRowSeats = allocatedSeats.Any(currentRowSeats.Contains) ? nextRowSeats : currentRowSeats;            
            if (remainingAdult !=null && remainingChild != null)
            {
                allocatedSeats.AddRange(TryAllocateChildAndAdultInRow(remainingChild, new List<Passenger> { remainingAdult }, remainingRowSeats));

            }
        }

        return allocatedSeats;
    }

    /// <summary>
    /// Attempts to allocate seats for a family with two children and two adults across two consecutive rows.
    /// It initially tries to place an adult between two children (E A E configuration) in either the current
    /// or the next row. If successful, the remaining adult is allocated a seat in the other row. If the E A E
    /// configuration cannot be achieved, it attempts to seat one child next to an adult in each row. This method
    /// ensures each child is seated next to an adult for safety. The allocation process prioritizes seating arrangements
    /// that keep the family unit as close together as possible, while also adapting to the constraints of available seating.
    /// </summary>
    /// <param name="children">List of children in the family.</param>
    /// <param name="adults">List of adults in the family.</param>
    /// <param name="currentRowSeats">Available seats in the current row.</param>
    /// <param name="nextRowSeats">Available seats in the next row.</param>
    /// <returns>List of allocated seats if successful; otherwise, an empty list.</returns>
    public List<Seat> AllocateTwoChildren(List<Passenger> children, List<Passenger> adults, List<Seat> currentRowSeats, List<Seat> nextRowSeats)
    {
        List<Seat> allocatedSeats = new List<Seat>();
        Passenger remainingAdult = null;

        // Tentative de placer un adulte entre deux enfants dans l'une des deux rangées
        foreach (var adult in adults)
        {
            if (TryAllocateAdultBetweenChildren(children, adult, currentRowSeats))
            {
                allocatedSeats.AddRange(children.SelectMany(c => c.Seats));
                allocatedSeats.AddRange(adult.Seats);
                remainingAdult = adults.First(a => a != adult); // L'autre adulte
                break;
            }
            else if (TryAllocateAdultBetweenChildren(children, adult, nextRowSeats))
            {
                allocatedSeats.AddRange(children.SelectMany(c => c.Seats));
                allocatedSeats.AddRange(adult.Seats);
                remainingAdult = adults.First(a => a != adult); // L'autre adulte
                break;
            }
        }

        // Si l'allocation E A E échoue, tentez de placer un enfant à côté d'un adulte dans chaque rangée
        if (remainingAdult != null)
        {
            // Placez l'adulte restant dans la rangée qui n'a pas encore été utilisée pour E A E
            if (allocatedSeats.Count == 0)
            { // Si E A E n'a pas réussi dans aucune rangée
                foreach (var row in new List<List<Seat>> { currentRowSeats, nextRowSeats })
                {
                    allocatedSeats.AddRange(TryAllocateChildAndAdultInRow(children[0], new List<Passenger> { remainingAdult }, row));
                    children.RemoveAt(0); // Enlever l'enfant alloué
                    if (children.Count == 0) break; // Sortir si tous les enfants sont alloués
                }
            }
            else
            {
                // Placez l'adulte restant seul dans l'autre rangée
                List<Seat> remainingRowSeats = allocatedSeats.Any(s => currentRowSeats.Contains(s)) ? nextRowSeats : currentRowSeats;
                var seat = remainingRowSeats.FirstOrDefault(s => !s.IsOccupied);
                if (seat != null)
                {
                    remainingAdult.Seats.Add(seat);
                    seat.IsOccupied = true;
                    allocatedSeats.Add(seat);
                }
            }
        }

        return allocatedSeats;
    }

     public bool TryAllocateAdultBetweenChildren(List<Passenger> children, Passenger adult, List<Seat> rowSeats)
        {
            int requiredSeats = adult.Type == PassengerType.AdultRequiringTwoSeats ? 4 : 3;

            for (int i = 0; i <= rowSeats.Count - requiredSeats; i++)
            {
                // Vérifiez si les places consécutives nécessaires sont disponibles.
                if (rowSeats.Skip(i).Take(requiredSeats).All(s => !s.IsOccupied))
                {
                    // Allocation pour un adulte nécessitant deux places.
                    if (adult.Type == PassengerType.AdultRequiringTwoSeats)
                    {
                        children[0].Seats.Add(rowSeats[i]); // Premier enfant
                        adult.Seats.Add(rowSeats[i + 1]); // Première place pour l'adulte
                        adult.Seats.Add(rowSeats[i + 2]); // Deuxième place pour l'adulte
                        children[1].Seats.Add(rowSeats[i + 3]); // Second enfant
                                                                // Marquer les sièges comme occupés.
                        rowSeats[i].IsOccupied = true;
                        rowSeats[i + 1].IsOccupied = true;
                        rowSeats[i + 2].IsOccupied = true;
                        rowSeats[i + 3].IsOccupied = true;
                    }
                    else
                    {
                        // Allocation pour un adulte nécessitant une place.
                        children[0].Seats.Add(rowSeats[i]); // Premier enfant
                        adult.Seats.Add(rowSeats[i + 1]); // Adulte
                        children[1].Seats.Add(rowSeats[i + 2]); // Second enfant
                                                                // Marquer les sièges comme occupés.
                        rowSeats[i].IsOccupied = true;
                        rowSeats[i + 1].IsOccupied = true;
                        rowSeats[i + 2].IsOccupied = true;
                    }
                    return true; // Allocation réussie
                }
            }
            return false; // Allocation échouée
        }
    
    private List<Seat> AllocateChildAndAdult(Passenger child, List<Passenger> adults, List<Seat> currentRowSeats, List<Seat> nextRowSeats)
    {
        List<Seat> allocatedSeats =
        [
            // Tentez d'abord dans la rangée courante.
            .. TryAllocateChildAndAdultInRow(child, adults, currentRowSeats),
        ];
        // i put the child and the adult in the next row so i ll put the other adult in the currentRow
        if (allocatedSeats.Count == 0)
        {
            // Si la tentative échoue dans la rangée courante, essayez dans la rangée suivante.
            allocatedSeats.AddRange(TryAllocateChildAndAdultInRow(child, adults, nextRowSeats));
            var remainingAdult = adults.Where(m => m.Seats.Count == 0).ToList();

            // Tenter de placer l'adulte restant.
            if (remainingAdult != null)
            {
                allocatedSeats.AddRange(AllocateRemainingAdult(remainingAdult[0], currentRowSeats));
            }
        }
        else
        {
            var remainingAdult = adults.Where(m => m.Seats.Count == 0).ToList();

            // Tenter de placer l'adulte restant.
            if (remainingAdult != null)
            {
                allocatedSeats.AddRange(AllocateRemainingAdult(remainingAdult[0], nextRowSeats));
            }
        }
        

        return allocatedSeats;
    }

    private List<Seat> TryAllocateChildAndAdultInRow(Passenger child, List<Passenger> adults, List<Seat> rowSeats)
    {
        List<Seat> allocatedSeats = new List<Seat>();

        // Assurer que le siège pour l'enfant et l'adulte soit disponible avant l'allocation.
        for (int i = 0; i < rowSeats.Count - 1; i++)
        {
            if (!rowSeats[i].IsOccupied && !rowSeats[i + 1].IsOccupied)
            {
                var singleSeatAdult = adults.FirstOrDefault(a => a.Type == PassengerType.Adult);
                if (singleSeatAdult != null)
                {
                    // Allocation pour un adulte nécessitant une place et un enfant à côté
                    AllocateSeatsForSingleAdultAndChild(singleSeatAdult, child, rowSeats[i], rowSeats[i + 1]);
                    return new List<Seat> { rowSeats[i], rowSeats[i + 1] };
                }

                var twoSeatAdult = adults.FirstOrDefault(a => a.Type == PassengerType.AdultRequiringTwoSeats);
                if (twoSeatAdult != null && i + 2 < rowSeats.Count && !rowSeats[i + 2].IsOccupied)
                {
                    // Allocation pour un adulte nécessitant deux places et un enfant à côté
                    AllocateSeatsForTwoSeatAdultAndChild(twoSeatAdult, child, rowSeats[i], rowSeats[i + 1], rowSeats[i + 2]);
                    return new List<Seat> { rowSeats[i], rowSeats[i + 1], rowSeats[i + 2] };
                }
            }
        }

        // Aucune allocation faite si les conditions ne sont pas remplies
        return allocatedSeats;
    }

    private void AllocateSeatsForSingleAdultAndChild(Passenger adult, Passenger child, Seat adultSeat, Seat childSeat)
    {
        adult.Seats.Add(adultSeat);
        child.Seats.Add(childSeat);
        adultSeat.IsOccupied = true;
        childSeat.IsOccupied = true;
    }

    private void AllocateSeatsForTwoSeatAdultAndChild(Passenger adult, Passenger child, Seat firstAdultSeat, Seat secondAdultSeat, Seat childSeat)
    {
        adult.Seats.Add(firstAdultSeat);
        adult.Seats.Add(secondAdultSeat);
        child.Seats.Add(childSeat);
        firstAdultSeat.IsOccupied = true;
        secondAdultSeat.IsOccupied = true;
        childSeat.IsOccupied = true;
    }

    private List<Seat> AllocateRemainingAdult(Passenger adult, List<Seat> rowSeats)
    {
        List<Seat> allocatedSeats = new List<Seat>();

        // Si l'adulte nécessite seulement une place.
        if (adult.Type == PassengerType.Adult)
        {
            var seat = rowSeats.FirstOrDefault(s => !s.IsOccupied);
            if (seat != null)
            {
                adult.Seats.Add(seat);
                seat.IsOccupied = true;
                allocatedSeats.Add(seat);
            }
        }
        // Si l'adulte nécessite deux places.
        else if (adult.Type == PassengerType.AdultRequiringTwoSeats)
        {
            for (int i = 0; i < rowSeats.Count - 1; i++)
            {
                if (!rowSeats[i].IsOccupied && !rowSeats[i + 1].IsOccupied)
                {
                    adult.Seats.AddRange(new[] { rowSeats[i], rowSeats[i + 1] });
                    rowSeats[i].IsOccupied = true;
                    rowSeats[i + 1].IsOccupied = true;
                    allocatedSeats.AddRange(new[] { rowSeats[i], rowSeats[i + 1] });
                    break;
                }
            }
        }

        return allocatedSeats;
    }
    public bool CanFamilyFitInSingleRow(Family family, List<Seat> rowSeats)
    {
        int continuousAvailableSeats = MaxContinuousSeats(rowSeats);
        int totalChildren = family.GetChildrenCount();
        int totalAdultsRequiringTwoSeats = family.GetAdultTwoSeatsCount();
        if (continuousAvailableSeats >= totalChildren+ totalAdultsRequiringTwoSeats)
        {
            return true;
        }
        return false;
    }

    private int MaxContinuousSeats(List<Seat> seats)
    {
        int maxCount = 0;
        int currentCount = 0;
        foreach (var seat in seats)
        {
            if (!seat.IsOccupied)
            {
                currentCount++;
                maxCount = Math.Max(maxCount, currentCount);
            }
            else
            {
                currentCount = 0; // Reset count if the current seat is occupied
            }
        }
        return maxCount;
    }

    public List<Seat> AllocateFamilySeatsInRow(Family family, List<Seat> rowSeats)
    {
        List<Seat> allocatedSeats = new List<Seat>();
        List<Seat> tempAvailableSeats = rowSeats.Where(s => !s.IsOccupied).OrderBy(s => s.Column).ToList();

        // Allocate seats for adults requiring two seats first
        foreach (var adult in family.Members.Where(m => m.Type == PassengerType.AdultRequiringTwoSeats))
        {
            for (int i = 0; i < tempAvailableSeats.Count - 1; i++)
            {
                if (tempAvailableSeats[i].Column + 1 == tempAvailableSeats[i + 1].Column)
                {
                    allocatedSeats.Add(tempAvailableSeats[i]);
                    allocatedSeats.Add(tempAvailableSeats[i + 1]);
                    adult.AddSeat(tempAvailableSeats[i]);
                    adult.AddSeat(tempAvailableSeats[i + 1]);
                    tempAvailableSeats.RemoveRange(i, 2); // Remove allocated seats
                    break;
                }
            }
        }

        // Allocate seats for regular adults and children, ensuring children are next to an adult
        foreach (var member in family.Members.Where(m => m.Type != PassengerType.AdultRequiringTwoSeats))
        {
            if (tempAvailableSeats.Any())
            {
                allocatedSeats.Add(tempAvailableSeats.First());
                member.AddSeat(tempAvailableSeats.First());
                tempAvailableSeats.RemoveAt(0);
            }
        }

        // Verify if allocation is complete and update seat occupancy
        if (allocatedSeats.Count == family.GetTotalSeatsRequired())
        {
            foreach (var seat in allocatedSeats)
            {
                seat.IsOccupied = true;
            }
            return allocatedSeats;
        }
        else
        {
            // If allocation failed, revert IsOccupied changes and return null
            foreach (var seat in allocatedSeats)
            {
                seat.IsOccupied = false; // Revert because the method signature does not allow for partial success
                foreach (var member in family.Members)
                {
                    member.Seats.Remove(seat); // Remove this seat from member's list
                }
            }
            return null; // Indicating unsuccessful allocation
        }
    }
    public bool CanFamilyBeSplitAcrossRows(Family Family, List<Seat> CurrentRowSeats, List<Seat> NextRowSeats)
    {
        // Assuming the family composition is known and includes up to 2 adults and 3 children, 
        // with the possibility of adults requiring two seats.
        int TotalAdults = Family.GetAdultCount();
        int Children = Family.GetChildrenCount();
        int AdultsRequiringTwoSeats = Family.GetAdultTwoSeatsCount();
       
        // If we cant put an adult and each row return false
        if (TotalAdults < 2 || AdultsRequiringTwoSeats > 0 && (CurrentRowSeats.Count(s => !s.IsOccupied) < 2 || NextRowSeats.Count(s => !s.IsOccupied) < 2))
        {
            return false;
        }

        // Calculate the minimum number of adults required to supervise children in each row.
        int MinAdultsForChildrenSupervision = Children > 0 ? 1 : 0;
        
        // Check if there's at least one adult for each row if children are to be seated in that row.
        // This is a simplified check and assumes that at least one row will have children if the family is split.
        bool EnoughAdultsForEachRow = TotalAdults >= MinAdultsForChildrenSupervision * 2 || (TotalAdults == 1 && Children == 0);

        int TotalSeatsNeeded = Family.GetTotalSeatsRequired();

        // Check for continuous seat blocks in each row for adults requiring two seats.
        bool hasContinuousSeatsForTwoSeatAdults = HasContinuousSeatsForTwoSeatAdults(CurrentRowSeats, AdultsRequiringTwoSeats) ||
                                                  HasContinuousSeatsForTwoSeatAdults(NextRowSeats, AdultsRequiringTwoSeats);

        //  // Check if it's possible to arrange the family with at least one adult in each row if needed.
        int totalSeatsAvailable = CurrentRowSeats.Count(s => !s.IsOccupied) + NextRowSeats.Count(s => !s.IsOccupied);
        return totalSeatsAvailable >= TotalSeatsNeeded && EnoughAdultsForEachRow && hasContinuousSeatsForTwoSeatAdults;
    }

    // Helper method to check for continuous seat blocks suitable for adults requiring two seats.
    private bool HasContinuousSeatsForTwoSeatAdults(List<Seat> rowSeats, int adultsRequiringTwoSeats)
    {
        if (adultsRequiringTwoSeats == 0) return true; // No specific requirement for continuous seats.

        int continuousSeatPairs = 0; // Tracks pairs of continuous seats available.
        int continuousSeats = 0; // Tracks current streak of continuous seats.

        foreach (var seat in rowSeats.OrderBy(s => s.Column))
        {
            if (!seat.IsOccupied) continuousSeats++;
            else
            {
                // Every time we encounter a break (an occupied seat), check if the streak
                // included pairs and then reset the continuous seats counter.
                continuousSeatPairs += continuousSeats / 2;
                continuousSeats = 0;
            }
        }

        // Check last streak of continuous seats if not ended by an occupied seat.
        continuousSeatPairs += continuousSeats / 2;

        // Verify if the total number of seat pairs meets or exceeds the requirement.
        return continuousSeatPairs >= adultsRequiringTwoSeats;
    }

    private List<Seat> FindSeatForPassenger(Passenger passenger, List<Seat> allSeats, ref int remainingSeats)
    {
        if (passenger.Seats != null && passenger.Seats.Any())
        {
            return passenger.Seats; // Les sièges sont déjà attribués à ce passager.
        }

        List<Seat> foundSeats = new List<Seat>();

        for (int i = 0; i < allSeats.Count; i++)
        {
            Seat seat = allSeats[i];
            if (!seat.IsOccupied)
            {
                if (passenger.Type == PassengerType.AdultRequiringTwoSeats && i + 1 < allSeats.Count &&
                    allSeats[i + 1].Row == seat.Row && !allSeats[i + 1].IsOccupied)
                {
                    foundSeats.Add(seat);
                    foundSeats.Add(allSeats[i + 1]);
                    seat.IsOccupied = true;
                    allSeats[i + 1].IsOccupied = true;
                    break;
                }
                else if (passenger.Type != PassengerType.AdultRequiringTwoSeats)
                {
                    foundSeats.Add(seat);
                    seat.IsOccupied = true;
                    break;
                }
            }
        }

        remainingSeats -= foundSeats.Count;
        return foundSeats;
    }
    public void DisplaySeatAssignment(List<Passenger> passengers, List<Family> families)
    {
        Console.WriteLine("Optimal seat distribution of passengers and families in the aircraft:");

        double totalRevenue = 0;
        int seatCount = 0;

        // Display seat information for each passenger
        foreach (var passenger in passengers)
        {
            if (passenger.Seats != null && passenger.Seats.Any())
            {
                string seatDetails = string.Join(", ", passenger.Seats.Select(s => $"R{s.Row + 1}C{s.Column + 1}"));
                Console.WriteLine($"Passenger {passenger.Id}: {passenger.Type} - Seat(s): {seatDetails} - Ticket Price: {passenger.GetTicketPrice():C2}");
                totalRevenue += passenger.GetTicketPrice();
                seatCount += passenger.Seats.Count;
            }
        }
       
        // Display seat information for each family
        foreach (var family in families)
        {
            int familySeatCount = 0;
            double familyRevenue = 0;
            if (family.AllocatedSeats != null && family.AllocatedSeats.Any())
            {
                Console.WriteLine($"Family {family.Id}:");
                
                foreach (var member in family.Members)
                {

                    {
                        Console.WriteLine($"  - Member {member.Id}: {member.Type}  - Ticket Price: {member.GetTicketPrice():C2}");
                        familyRevenue += member.GetTicketPrice();
                        
                    }
                }
                familySeatCount +=family.AllocatedSeats.Count();
                
                var sb = new StringBuilder($"Family {family.Id} - Allocated Seats:\n");
                    foreach (var seat in  family.AllocatedSeats)
                    {
                        sb.AppendLine($"- Row: {seat.Row + 1}, Column: {seat.Column + 1}");
                    }
                Console.WriteLine(sb);
            

            Console.WriteLine($"    Total Seats: {family.AllocatedSeats.Count}");
            Console.WriteLine($"    Total Revenue Generated: {familyRevenue:C2}");
            
            }
            totalRevenue += familyRevenue;
            seatCount += familySeatCount;
        }

        Console.WriteLine($"\nTotal Seats: {seatCount}");
        Console.WriteLine($"Total Revenue Generated: {totalRevenue:C2}");
    }
}
