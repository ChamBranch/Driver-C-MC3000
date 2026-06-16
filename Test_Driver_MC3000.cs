// See https://aka.ms/new-console-template for more information
//usage du namespace et des classe de claude
using static MC3000Device;

Console.WriteLine("Hello, World!");

MC3000Device mc3000 = null; // <-- déclaration déplacée hors du try

try
{
    mc3000 = new MC3000Device();
    Console.WriteLine("Connexion...");
    mc3000.Connect();

    Console.WriteLine("MC3000 connecté !");

    Console.WriteLine("Appuyez sur une touche pour quitter...");

}
catch (Exception)
{
    Console.WriteLine($"Erreur : non connecté");
}

if (mc3000 != null)
{
    mc3000.Connect();
    var status = mc3000.CommandReadSlotStatus(1); // <-- appel sur l'instance
    Console.WriteLine($"  Slot n°         : {status.Slot + 1}");
    Console.WriteLine($"  Capacité        : {status.CapacityMilliAh} mAh");
    Console.WriteLine($"  Tension         : {status.VoltageMilliV} mV");
    Console.WriteLine($"  Temperature     : {status.TemperatureTenth / 10} °C");

    Thread.Sleep(10);

    mc3000.CommandMC3000State();

    var config = new SlotConfig
    {
        SlotNumber = 0,
        BatteryType = 1,
        Mode = 1,
        CapacityMilliAh = 1450,
        ChargeCurrentMilliA = 890,
        DischargeCurrentMilliA = 630,
        ChargeEndVoltage = 3210,
        ChargeEndCurrent = 200,
        DischargeEndCurrent = 70,
        CycleCount = 5,
        CycleMode = 2,
        RestingTime = 10,
        NiDeltaMV = 10,
        HoldVoltage = 4170,
        CutOffTemperature = 60,
        CutOffTime = 30
    };


    mc3000.CommandWriteSlotConfig(config);

    Thread.Sleep(10);//pause de 10ms

    mc3000.Connect();

    var slot = mc3000.CommandReadSlotConfig(0);
    Console.WriteLine($" slot: {slot.SlotNumber + 1}");
    Console.WriteLine($" Batterie type: {slot.BatteryType}");
    Console.WriteLine($" cycle mode: {slot.Mode}");
    Console.WriteLine($" Capa: {slot.CapacityMilliAh}");
    Console.WriteLine($" Charge current : {slot.ChargeCurrentMilliA}");
    Console.WriteLine($" Discharge : {slot.DischargeCurrentMilliA}");
    Console.WriteLine($" Charge End Voltage : {slot.ChargeEndVoltage}");
    Console.WriteLine($" Charge End Current : {slot.ChargeEndCurrent}");
    Console.WriteLine($" Disharge End Current : {slot.DischargeEndCurrent}");
    Console.WriteLine($" Cycle count : {slot.CycleCount}");
    Console.WriteLine($" mode cycle : {slot.CycleMode}");
    Console.WriteLine($" Resting time : {slot.RestingTime}");
    Console.WriteLine($" Ni delt mV : {slot.NiDeltaMV}");
    Console.WriteLine($" hold voltage : {slot.HoldVoltage}");
    Console.WriteLine($" Cut off temp: {slot.CutOffTemperature}");
    Console.WriteLine($" cur off time : {slot.CutOffTime}");


    mc3000.Disconnect();
}

