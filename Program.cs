using AirPlaneTicketManagement.Contracts;
using AirPlaneTicketManagement.Data;
using AirPlaneTicketManagement.Services;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var builder = Host.CreateDefaultBuilder(args)
    .ConfigureServices((hostContext, services) =>
    {
        // Add your services to the container.
        services.AddSingleton<IPassengerService, PassengerService>();
        services.AddSingleton<ISeatAssignementService,SeatAssignementService>();
    });

var host = builder.Build();


using (var serviceScope = host.Services.CreateScope())
{
    var services = serviceScope.ServiceProvider;

    try
    {
        var PassengerService = services.GetRequiredService<IPassengerService>();
        var SeatAssignementService = services.GetRequiredService<ISeatAssignementService>();

        // Générer les passagers et les familles
        var (Passengers, Families) = PassengerService.GeneratePassengersAndFamilies();
        PassengerService.DisplayPassengersAndFamilies(Passengers, Families);
        
        // Attribuer les sièges aux passagers et aux familles
        SeatAssignementService.AssignSeats(Passengers, Families);

        // Afficher la répartition optimale des passagers et des familles dans l'avion ainsi que le chiffre d'affaires total généré
        SeatAssignementService.DisplaySeatAssignment(Passengers, Families);
    }
    catch (Exception ex)
    {
        Console.WriteLine($"An error occurred: {ex.Message}");
    }
}
