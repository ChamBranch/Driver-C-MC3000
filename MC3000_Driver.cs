using HidSharp; //Librairie pour communiquer avec les périphériques HID

/// <summary>
/// Classe permetant d'exploiter le MC3000 via le protocole HID. Elle permet de se connecter, 
/// de lire l'état des slots, de lire et écrire la configuration des slots, de démarrer et arrêter le traitement des slots, 
/// et d'obtenir l'état du MC3000.
/// </summary>

public class MC3000Device
{
    //Connect
    private const int VendorId = 0x0000;//ID du fabricant du MC3000
    private const int ProductId = 0x0001;
    private HidDevice _device;
    private HidStream _stream;

    //Construction Paquet et Cheksum
    private const byte PACKET_START = 0x0F;
    private const byte PACKET_END = 0xFF;
    private const int PACKET_SIZE = 64;   // Toujours 64 octets

    //FONCTIONELLE : pour se connecter au MC3000, elle cherche le périphérique HID correspondant et ouvre un flux de communication.
    public void Connect()
    {
        // On cherche le MC3000 parmi tous les périphériques HID connectés
        var deviceList = DeviceList.Local;
        _device = deviceList.GetHidDeviceOrNull(VendorId, ProductId);

        if (_device == null)
        {
            Console.WriteLine("MC3000 non trouvé. Vérifiez la connexion USB.");
            throw new Exception("MC3000 non trouvé. Vérifiez la connexion USB."); //L'exception fait sortir du if
        }

        _stream = _device.Open();
        _stream.ReadTimeout = 2000; // 2 secondes de timeout
        _stream.WriteTimeout = 2000;
    }

    //FONCTIONELLE ferme bien le stream le rendant innacessible.
    public void Disconnect()
    {
        _stream?.Close();//si _stream n'est pas null, on le ferme
    }

    //FONCTIONNELLE Calcul du checksum en sommant les octets de la partie variable du paquet et en prenant le résultat modulo 256.
    private byte CalculateChecksum(byte[] data, int startIndex, int endIndex)
    {
        int sum = 0;
        for (int i = startIndex; i < endIndex; i++)
            sum += data[i];
        return (byte)(sum % 256);
    }


    //FONCTIONNELLE : Construit un paquet de 64 octets avec padding
    private byte[] BuildPacket(byte subCommand, byte slot)
    {
        byte[] packet = new byte[PACKET_SIZE]; // Créé un Paquet de 64 octets initialisé à 0x00

        if (slot < 0 || slot > 3)
            throw new ArgumentOutOfRangeException(nameof(slot), slot, "Le numéro de slot doit être entre 0 et 3.");

        packet[0] = PACKET_START;  // 0x0F
        packet[1] = 0x04;          // longueur de la partie variable avant 0x04
        packet[2] = subCommand;    // ex: 0x55, 0x5F, 0x5A
        packet[3] = 0x00;
        packet[4] = slot;          // numéro de slot : 0, 1, 2 ou 3
        packet[5] = CalculateChecksum(packet, 2, 5); // checksum sur octets 2 à 4
        packet[6] = PACKET_END;    // 0xFF
        packet[7] = PACKET_END;    // 0xFF
                                   // Les octets 8 à 63 restent à 0x00 (padding)

        return packet;
    }

    //FONCTIONELLE Envoie un paquet et retourne la réponse de 64 octets
    public byte[] SendAndReceive(byte[] packet)
    {
        // HidSharp nécessite parfois un octet "Report ID" = 0x00 en préfixe
        byte[] toSend = new byte[PACKET_SIZE + 1];
        toSend[0] = 0x00; // Report ID
        Array.Copy(packet, 0, toSend, 1, packet.Length);

        _stream.Write(toSend);

        // On lit la réponse (64 octets)
        byte[] response = new byte[PACKET_SIZE + 1]; // +1 pour le Report ID en retour
        _stream.Read(response);

        // On retourne les 64 octets utiles (sans le Report ID)
        byte[] result = new byte[PACKET_SIZE];
        Array.Copy(response, 1, result, 0, PACKET_SIZE);
        return result;
    }

    // Structure pour stocker l'état de charge d'un slot à un instant T
    public class SlotStatus
    {
        public int Slot { get; set; }
        public int BatteryType { get; set; }
        public int WorkStatus { get; set; }  // 1=Charge, 4=Terminé
        public int WorkTime { get; set; }  // en secondes
        public int VoltageMilliV { get; set; }  // en mV
        public int CurrentMilliA { get; set; }  // en mA
        public int CapacityMilliAh { get; set; }  // en mAh
        public int TemperatureTenth { get; set; }  // en dixièmes de degré
        public int InnerResistance { get; set; }  // en mΩ
    }


    //FONCTIONNELLE pour lire l'état d'un slot spécifique à un instant T en envoyant une commande de lecture et en interprétant la réponse pour remplir une structure SlotStatus.
    public SlotStatus CommandReadSlotStatus(int slotNumber) // slotNumber : 0, 1, 2 ou 3
    {
        try
        {
            // Opcode 0x55 = lecture de l'état en cours
            byte[] request = BuildPacket(0x55, (byte)slotNumber);
            byte[] data = SendAndReceive(request);

            var status = new SlotStatus
            {
                Slot = data[1],
                BatteryType = data[2],
                WorkStatus = data[4],
                WorkTime = ReadInt16BigEndian(data, 6),
                VoltageMilliV = ReadInt16BigEndian(data, 8),
                CurrentMilliA = ReadInt16BigEndian(data, 10),
                CapacityMilliAh = ReadInt16BigEndian(data, 12),
                TemperatureTenth = ReadInt16BigEndian(data, 14),
                InnerResistance = ReadInt16BigEndian(data, 16),
            };

            return status;
        }

        catch (ArgumentOutOfRangeException ex)
        {
            Console.WriteLine($"Erreur : {ex.Message}");
            return null;
        }



    }

    //FONCTIONELLE : Convertit deux octets consécutifs d'un tableau en un entier en utilisant l'ordre big-endian (octet de poids fort suivi de l'octet de poids faible).
    private static int ReadInt16BigEndian(byte[] data, int index)
    {
        return (data[index] << 8) | data[index + 1];
    }
    // FONCTIONELLE écris un entier sur 2 octets dans un tableau en utilisant l'ordre big-endian (octet de poids fort suivi de l'octet de poids faible).
    private void WriteInt16BigEndian(byte[] data, int index, int value)
    {
        data[index] = (byte)((value >> 8) & 0xFF); // octet de poids fort
        data[index + 1] = (byte)(value & 0xFF);        // octet de poids faible
    }

    public class SlotConfig
    {
        public int SlotNumber { get; set; }
        public int BatteryType { get; set; }  // 0=LiIon, 1=LiFe, 3=NiMH...
        public int Mode { get; set; }  // 0=Charge, 3=Discharge...
        public int CapacityMilliAh { get; set; }
        public int ChargeCurrentMilliA { get; set; }
        public int DischargeCurrentMilliA { get; set; }
        public int DischargeEndVoltage { get; set; }
        public int ChargeEndCurrent { get; set; }
        public int DischargeEndCurrent { get; set; }
        public int CycleCount { get; set; }
        public int RestingTime { get; set; }
        public int CycleMode { get; set; } //Compris entre 0 et 3,  0=Cycle charge-décharge, 1=Cycle charge-décharge-charge, 2=Cycle décharge-charge, 3=Cycle décharge-charge-décharge
        public int NiDeltaMV { get; set; }
        public int Trickle { get; set; }
        public int HoldVoltage { get; set; }
        public int CutOffTemperature { get; set; }
        public int CutOffTime { get; set; }


        public int ChargeEndVoltage { get; set; }

    }
    //FONCTIONELLE : Lit la configuration d'un slot spécifique en envoyant une commande de lecture et en interprétant la réponse pour remplir une structure SlotConfig.
    public SlotConfig CommandReadSlotConfig(int slotNumber)
    {
        // Opcode 0x5F = lecture de la configuration du slot
        byte[] request = BuildPacket(0x5F, (byte)slotNumber);
        byte[] data = SendAndReceive(request);

        //PrintBytes(data, 32);//OPTIONNEL : affiche les 32 premiers octets de la réponse pour debug

        var config = new SlotConfig
        {
            SlotNumber = data[1],//ok
            BatteryType = data[3],//ok
            Mode = data[4],//ok
            CapacityMilliAh = ReadInt16BigEndian(data, 5),//ok
            ChargeCurrentMilliA = ReadInt16BigEndian(data, 7),//ok
            DischargeCurrentMilliA = ReadInt16BigEndian(data, 9),//ok
            DischargeEndVoltage = ReadInt16BigEndian(data, 11),//ok
            ChargeEndVoltage = ReadInt16BigEndian(data, 13),//ok
            ChargeEndCurrent = ReadInt16BigEndian(data, 15),//ok
            DischargeEndCurrent = ReadInt16BigEndian(data, 17),//ok
            CycleCount = data[19],//ok
            RestingTime = data[20],//ok
            CycleMode = data[21],//ok
            NiDeltaMV = data[22],//ok
            Trickle = data[23],//ok
            HoldVoltage = ReadInt16BigEndian(data, 24),//ok
            CutOffTemperature = data[26],//ok
            CutOffTime = ReadInt16BigEndian(data, 27),//ok
        };

        return config;
    }
    //FONCTIONELLE : Écrit la configuration d'un slot spécifique en construisant un paquet avec les paramètres fournis dans une structure SlotConfig et en l'envoyant au MC3000.
    public void CommandWriteSlotConfig(SlotConfig data)
    {
        byte[] packet = new byte[PACKET_SIZE];
        packet[0] = PACKET_START;
        packet[1] = 0x20; //longeur partie variable (octets de commande)
        packet[2] = 0x11; //commande programmation d'un slot
        packet[3] = 0x00; //tjr à 0
        packet[4] = (byte)(data.SlotNumber);//ok
        packet[5] = (byte)(data.BatteryType);//ok
        WriteInt16BigEndian(packet, 6, data.CapacityMilliAh);//ok
        packet[8] = (byte)(data.Mode);//ok
        WriteInt16BigEndian(packet, 9, data.ChargeCurrentMilliA);//ok
        WriteInt16BigEndian(packet, 11, data.DischargeCurrentMilliA);//ok
        WriteInt16BigEndian(packet, 13, data.DischargeEndVoltage);//ok
        WriteInt16BigEndian(packet, 15, data.ChargeEndVoltage);//ok
        WriteInt16BigEndian(packet, 17, data.ChargeEndCurrent);//ok
        WriteInt16BigEndian(packet, 19, data.DischargeEndCurrent);//ok
        packet[21] = (byte)(data.CycleCount);//ok
        packet[22] = (byte)(data.RestingTime);//ok
        packet[23] = 0x00; //à voir
        packet[24] = (byte)(data.CycleMode);//ok
        packet[25] = (byte)(data.NiDeltaMV);//ok
        packet[26] = (byte)(data.Trickle);//ok
        packet[27] = 0x00; //à voir
        packet[28] = (byte)(data.CutOffTemperature);//ok
        WriteInt16BigEndian(packet, 29, data.CutOffTime);//ok
        WriteInt16BigEndian(packet, 31, data.HoldVoltage);//ok
        packet[33] = CalculateChecksum(packet, 2, 33);//ok
        packet[34] = 0xFF;
        packet[35] = 0xFF;

        byte[] toSend = new byte[PACKET_SIZE + 1];
        toSend[0] = 0x00;
        Array.Copy(packet, 0, toSend, 1, packet.Length);
        _stream.Write(toSend);

        PrintBytes(packet, 32);//OPTIONNEL : affiche les 32 premiers octets de la réponse pour debug
    }

    // FONCTIONELLE : lit la version majeur et mineur du firmware du MC3000.
    public void CommandMC3000State()
    {
        // Opcode 0x5A = paramètres système
        byte[] packet = new byte[PACKET_SIZE];
        packet[0] = PACKET_START;
        packet[1] = 0x04;
        packet[2] = 0x5A;
        packet[3] = 0x00;
        packet[4] = 0x00;
        packet[5] = 0x5A; // checksum = 0x5A
        packet[6] = PACKET_END;
        packet[7] = PACKET_END;

        byte[] data = SendAndReceive(packet);

        PrintBytes(data, 64);//OPTIONNEL : affiche les 32 premiers octets de la réponse pour debug

        // La version logicielle est sur 2 octets : data[20] (high) et data[21] (low)
        int softwareVersionMajor = data[27];
        int softwareVersionMinor = data[28];
        Console.WriteLine($"Version firmware : {softwareVersionMajor}.{softwareVersionMinor}");
    }
    //FONCTIONELLE : Envoie une commande fixe pour démarrer le traitement des slots à partir des programmes présents sur ces derniers.
    public void CommandStartProcessing()
    {
        // Commande fixe : 0F 03 05 00 05 FF FF FF + padding
        byte[] packet = new byte[PACKET_SIZE];
        packet[0] = PACKET_START;
        packet[1] = 0x03;
        packet[2] = 0x05;
        packet[3] = 0x00;
        packet[4] = 0x05; // checksum = 0x05
        packet[5] = 0xFF;
        packet[6] = 0xFF;
        // bytes 7-63 restent à 0x00

        byte[] toSend = new byte[PACKET_SIZE + 1];
        toSend[0] = 0x00;
        Array.Copy(packet, 0, toSend, 1, packet.Length);
        _stream.Write(toSend);
    }
    //FONCTIONELLE: Arrête le traitement des slots en cours en envoyant une commande fixe.
    public void CommandStopProcessing()
    {
        // Commande fixe : 0F 03 FE 00 FE FF FF FF + padding
        byte[] packet = new byte[PACKET_SIZE];
        packet[0] = PACKET_START;
        packet[1] = 0x03;
        packet[2] = 0xFE;
        packet[3] = 0x00;
        packet[4] = 0xFE;//checksum = 0xFE
        packet[5] = 0xFF;
        packet[6] = 0xFF;

        byte[] toSend = new byte[PACKET_SIZE + 1];
        toSend[0] = 0x00;
        Array.Copy(packet, 0, toSend, 1, packet.Length);
        _stream.Write(toSend);
    }
    // FONCTIONELLE : Affiche les octets d'un tableau en hexadécimal pour le débogage.
    public void PrintBytes(byte[] data, int count)
    {
        count = Math.Min(count, data.Length);
        for (int i = 0; i < count; i++)
            Console.Write($"{data[i]:X2} ");
        if (count < data.Length)
            Console.Write("...");
        Console.WriteLine();
    }
}

