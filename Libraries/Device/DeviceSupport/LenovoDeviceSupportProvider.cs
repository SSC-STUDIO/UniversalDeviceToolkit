namespace UniversalDeviceToolkit.Lib.DeviceSupport;

public sealed class LenovoDeviceSupportProvider : CatalogDeviceSupportProvider
{
    private static readonly string[] LenovoHardwareEnabledFeatures =
    [
        "lenovo-hardware-controls",
        "sensors",
        "power-modes",
        "battery",
        "system-optimization"
    ];

    private static readonly string[] UniversalBasicEnabledFeatures =
    [
        "system-optimization",
        "language",
        "theme",
        "updates",
        "logs"
    ];

    private static readonly string[] UniversalBasicHiddenFeatures =
    [
        "lenovo-hardware-controls",
        "power-modes",
        "keyboard-backlight",
        "god-mode",
        "gpu-overclock",
        "fan-curve"
    ];

    private static readonly DeviceSupportCatalog BuiltInCatalog = new()
    {
        SchemaVersion = 1,
        AppVersion = "built-in",
        // Order: more specific packs first so MTM FirstOrDefault does not steal into Legion 5.
        // Each machine type should appear in only one pack.
        DevicePacks =
        [
            LenovoHardwarePack(
                "lenovo-legion-pro-7",
                "Lenovo Legion Pro 7",
                ["Legion"],
                ["16IAX", "16IRX", "16IRX10", "16IAX10", "16IAX10H", "16ARX", "16ARX10", "16IRX9", "16IAX9", "16IRX11", "16IAX11", "16IRX12", "16IAX12", "18IRX", "18IAX"],
                // Pro 7 / Y9000K / 至尊版 — PSREF-verified set only:
                // 83F5 = Pro 7 16IAX10H (Y9000P 2025 至尊版), 83RU = Pro 7 16AFR10H,
                // 83DE = Pro 7 16IRX9H, 82W* = 2022 Pro 7. (83F4/83F6 are Pro 5-class,
                // 83FD is Legion 7, 83S0 is LOQ — do not re-add them here.)
                ["83RU", "83F5", "83DE", "82WR", "82WQ", "82WS", "82WT"],
                // Avoid bare Y9000P/IRX keywords here — those belong to Pro 5 (e.g. 83DF IRX9).
                ["Legion Pro 7", "Legion Pro 7i", "Legion Pro 7 16", "Legion Pro 7 18", "Y9000K", "R9000K",
                 "Y9000K 2024", "Y9000K 2025", "Y9000K 2026", "R9000K 2024", "R9000K 2025", "R9000K 2026",
                 "拯救者 Y9000K", "拯救者 R9000K", "Y9000P Ultimate", "Pro 7i"]),
            LenovoHardwarePack(
                "lenovo-legion-pro-5",
                "Lenovo Legion Pro 5",
                ["Legion"],
                ["16IAX", "16IRX", "16IRX10", "16IAX10", "16IAX10H", "16ARX", "16ARX10", "16IRX9", "16IAX9", "16ARP", "16ARH", "16ADR", "16AFR", "16ADR10", "16AFR10", "16IRX11", "16IAX11", "16IRX12", "16IAX12", "16ARP10", "16ARH10"],
                // 83DF = Legion Y9000P IRX9 (CN) — full hardware pack.
                // PSREF-verified Gen 10 additions: 83NN 16IRX10 (Y7000P IRX10 2025),
                // 83F3 16IAX10, 83F2 16AFR10, 83LT 16ADR10, 83LU 16IAX10H;
                // China-verified: 83F4 (Y9000P IAX10), 83F6 (R9000P AFR10),
                // 83LV/83RV (R9000P ADR10/ADR10H), 83QC/83QD (R9000P ADR10M/AFR10M),
                // 83QF (Y9000P IRX11/IPX11 2026).
                ["83LT", "83F3", "83DF", "83F2", "83LU", "82WM", "83NN", "82WK", "82JQ", "82RF", "82RG", "83DG", "83LR", "83LS", "83LV", "83LW", "83LX", "82SN", "82SM",
                 "83F4", "83F6", "83RV", "83QC", "83QD", "83QF",
                 // Legion 5 Pro 16" 2021: 82JQ/82JS = 16ACH6(H) R9000P 2021(H),
                 // 82JD/82JF = 16ITH6(H) Y9000P 2021(H).
                 "82JS", "82JD", "82JF"],
                ["Legion Pro 5", "Legion Pro 5i", "Legion Pro 5 16", "Y9000P", "R9000P",
                 "Y9000P IRX9", "Y9000P IAX9", "Y9000P IRX10", "Y9000P IAX10", "Y9000P IRX11", "Y9000P IAX11",
                 "Y9000P 2023", "Y9000P 2024", "Y9000P 2025", "Y9000P 2026",
                 "R9000P 2023", "R9000P 2024", "R9000P 2025", "R9000P 2026",
                 "拯救者 Y9000P", "拯救者 R9000P", "Pro 5i"]),
            LenovoHardwarePack(
                "lenovo-legion-9",
                "Lenovo Legion 9",
                ["Legion"],
                ["16IRX", "16IAX", "18IAX", "16IRX9", "16IAX9", "18IRX", "18IAX10", "16IRX10", "16IAX10", "18IRX10", "16IRX11", "18IAX11"],
                // Verified: 83G0 = Legion 9 16IRX9 (Y9000K 2024), 83EY = Legion 9 18IAX10
                // (Gen 10 18", 拯救者创世), 83AG = Legion 9 16IRX8 (2023).
                ["83G0", "83EY", "83AG"],
                ["Legion 9", "Legion 9i", "Legion 9 16", "Legion 9 18", "拯救者 Legion 9", "拯救者创世"]),
            LenovoHardwarePack(
                "lenovo-legion-7",
                "Lenovo Legion 7",
                ["Legion"],
                ["16ACH", "16ARH", "16IAH", "16IAX", "16IRH", "16IRX", "16IRX9", "16IAX9", "16IRX10", "16IAX10", "16IRX11", "16IAX11"],
                // Verified: 83KY = Legion 7 16IAX10 (Y9000X 2025, 2026 refresh same MT),
                // 83FD = Legion 7 16IRX9 (Y9000X 2024), 83Q8 = Legion 7 16AGP11 (2026),
                // 83V9 = Legion 7 15ASH11 (R9000X 2026). (83L3 is Legion Go S, 83L0 is Yoga —
                // do not re-add them here.)
                // 2020–2021 (LLT-supported era): 81YT/81YU = Legion 7 15IMH05(H),
                // 81YV/81YW = Y9000K 2020(H), 82K6 = Y9000K 2021 (82N6 = R9000K 2021),
                // 81TH/81YY = Y9000X 2020(R), 82BD = Y9000X 2021, 82EH = Legion C7 15.
                ["83KY", "83FD", "83Q8", "83V9", "82UH", "82TD", "82N6", "82N7", "83FE", "83FF",
                 "81YT", "81YU", "81YV", "81YW", "82K6", "81TH", "81YY", "82BD", "82EH"],
                ["Legion 7", "Legion 7i", "Legion 7 16", "Legion Slim 7", "Legion 7a", "Legion C7", "Y9000X", "拯救者 Y9000X", "R9000X", "R9000X 2026", "Y9000K 2020", "Y9000K 2021", "拯救者 Legion 7"]),
            LenovoHardwarePack(
                "lenovo-legion-slim-5",
                "Lenovo Legion Slim 5",
                ["Legion", "Lenovo Slim"],
                ["14AHP", "14APH", "14AKP", "14IRP", "14IRX", "14IAX", "14AHP10", "14AKP10", "14IRX10", "14IAX10", "16AHP", "16APH", "16IRX", "16IAX", "16AHP10", "16APH10"],
                // Slim line ended at Gen 9 (2024). Verified: 83DH = 16AHP9 (R7000P 2024),
                // 83EX = 16ARP9, 82Y5 = 14APH8 (R9000X 2023), 82Y9 = 16APH8,
                // 82YA/82YB = 16IRH8 (Y7000P IRH8 2023).
                // Slim 7 2020–2021: 82K8 = S7 15ACH6 (R9000X 2021R), 82HN = R9000X 2021,
                // 82HM = S7-15ARH5, 82BC = S7-15IMH5.
                ["83DH", "83EX", "82Y5", "82Y9", "82YA", "82YB", "83D6", "83D0", "83D1",
                 "82K8", "82HN", "82HM", "82BC"],
                ["Legion Slim 5", "Legion Slim 5i", "Legion Slim 7", "Legion Slim 7i", "Legion S7", "R9000X 2021", "R9000X 2021R", "R9000X 2023", "拯救者 Slim"]),
            LenovoHardwarePack(
                "lenovo-legion-go",
                "Lenovo Legion Go",
                ["Legion"],
                ["NX", "8APU1", "NX10", "NX11", "8APU2"],
                // Verified: 83E1 = Go 8APU1, 83L3 = Go S 8ARP1, 83N6 = Go S 8APU1,
                // 83N0 = Go 2 8ASP2 — one MT per platform.
                ["83E1", "83N0", "83L3", "83N6"],
                ["Legion Go", "Legion Go S", "Legion Go 2", "Legion Go Gen 2", "拯救者 Go"]),
            LenovoHardwarePack(
                "lenovo-loq",
                "Lenovo LOQ",
                ["LOQ"],
                ["15IAX", "15IRH", "15IRX", "15ARP", "15APH", "15AHP", "15IAX9", "15IRX9", "15ARP9", "15APH11", "15IPH11", "15ARP10", "15IRX10", "15IAX10", "15APH10", "15IRX11", "15IAX11", "16IRH", "16IAX", "16APH", "16IRX", "16ARP", "16IRX10", "16IAX10", "17IRX", "17IAX", "17IRX9", "17IRX10", "17IRX11"],
                // Keep LOQ MTMs distinct from Legion 5/7.
                // Verified Gen 10/11: 83JE/83Q1 = 15IRX10, 83JG = 15AHP10, 83JH = 17IRX10,
                // 83S0 = Essential 15ARP10E, 83SC = Essential 15IRX11, 83VA = Essential 15ARP11,
                // 83SL = 15IPH11, 83TN = 15AHP11; Gen 8: 82XT/82XV/82XU/82XW.
                ["83GS", "83GT", "83GU", "83GV", "83GW", "83JC", "83JD", "83JE", "83JF", "83JG", "83JH", "82XV", "82XW", "82XT", "82XU", "83DV", "83DW", "83DX", "83DY", "83AQ", "83AR", "83AS",
                 "83Q1", "83S0", "83SC", "83VA", "83SL", "83TN"],
                ["LOQ", "LOQ 15", "LOQ 16", "LOQ 17", "LOQ 15IRX", "LOQ 15IAX", "LOQ 15APH", "LOQ Essential", "G5000", "拯救者 LOQ", "拯救者G5000", "拯救者 G5000"]),
            LenovoHardwarePack(
                "lenovo-legion-5",
                "Lenovo Legion 5",
                ["Legion"],
                ["15ACH", "15AKP", "15AHP", "15APH", "15ARH", "15ARP", "15IAH", "15IAX", "15IHU", "15IMH", "15IRH", "15IRX", "15ITH", "15ARP10", "15IRX10", "15IAX10", "15AKP10", "15AHP10", "15APH11", "15IRX11", "15IAX11", "16ACH", "16ADR", "16AFR", "16AHP", "16APH", "16ARH", "16ARP", "16ARX", "16IAH", "16IAX", "16IRH", "16IRX", "16ITH", "16IRX9", "16IRX10", "16IAX10", "16IRX11", "17ACH", "17ARH", "17IRX", "17ITH", "17IMH", "17IRX10", "17IRX11"],
                // Machine types cover 2020–2026 Legion 5 / Y7000 / R7000 family refreshes.
                // IdeaPad Gaming MTMs live in lenovo-ideapad-gaming only.
                // Verified additions: 83F0/83N2 15IAX10, 83F1 15AKP10, 83M0 15AHP10 (R7000 2025),
                // 83LY 15IRX10 (Y7000 2025), 83NX 16IAX10; Gen 11: 83RW 15IPH11,
                // 83QE/83VK 15IAX11, 83Q6 15AGP11, 83Q7 15AHP11.
                // (83JG/83JH are LOQ, 83Q8 is Legion 7, 83QC/83QD are Pro 5 — do not re-add.)
                ["83F0", "83F1", "83M0", "83NX", "83N2", "83LY", "83EW", "83EG", "83JJ", "82RC", "82RB", "82TB", "83EF", "82RE", "82RD", "82AX", "82B0", "82GR", "82JU", "82JV", "82JW", "82JY", "82K0", "82K1", "82K2", "82N4", "82N5", "82NW", "83FG", "83FH", "83G1", "83G2", "83LL", "83LM", "83LN", "83DT", "83DU", "83C6", "83C7",
                 "83Q6", "83Q7", "83RW", "83QE", "83VK",
                 "82B1", "82B2", "82B3", "82B4", "82B5",
                 // 2020: 82AU 5-15IMH05, 81Y6/82CF 5-15IMH05H, 82B3 17IMH05, 81Y8 17IMH05H,
                 // 82GN 17ARH05H, 82AY/82AW 5P-15IMH05(H), 82GU 5P-15ARH05H;
                 // CN 2020: 82AV Y7000, 81Y7 Y7000H, 82B6 R7000, 82B4 R7000H.
                 // 2021: 82JH Y7000P, 82JK Y7000.
                 "82AU", "81Y6", "82CF", "81Y8", "82GN", "82AY", "82AW", "82GU",
                 "82AV", "81Y7", "82B6",
                 "82JH", "82JK"],
                ["Legion 5", "Legion 5i", "Legion 5 Pro", "Legion 5a", "Legion 5 15", "Legion 5 16", "Legion 5 17",
                 "Y7000", "Y7000P", "Y7000X", "R7000X", "Y7000P 2020H", "Y7000P2020H", "Y7000P IRX9", "Y7000P IRX10", "Y7000P IRX11",
                 "Y7000 2023", "Y7000 2024", "Y7000 2025", "Y7000 2026", "Y7000P 2023", "Y7000P 2024", "Y7000P 2025", "Y7000P 2026",
                 "R7000", "R7000P", "R7000P 2021", "R7000P 2023", "R7000P 2024", "R7000P 2025", "R7000P 2026",
                 "拯救者 Y7000", "拯救者 Y7000P", "拯救者 Y7000X", "拯救者 R7000X", "拯救者 R7000", "拯救者 R7000P"]),
            LenovoBasicPack(
                "lenovo-chromebook-basic",
                "Lenovo Chromebook Basic",
                ["Chromebook", "IdeaPad"],
                [],
                [],
                ["Chromebook", "Chromebook Plus", "Flex Chromebook", "IdeaPad Chromebook"]),
            LenovoHardwarePack(
                "lenovo-ideapad-gaming",
                "Lenovo IdeaPad Gaming",
                ["IdeaPad Gaming", "IdeaPad"],
                ["15ACH", "15ARH", "15IAH", "15IAU", "15IRH", "15IHU", "15IMH", "16ACH", "16ARH", "16IAH", "16IRH", "16IAH7", "15IAH7"],
                // Verified: 82S9/82UJ = 15IAH7, 82SB/82UK = 15ARH7, 82SA = 16IAH7, 82SC = 16ARH7
                // (Gen 7 only — the line was succeeded by LOQ from 2023).
                // 81Y4 = ideapad Gaming 3-15IMH05 (2020); 81LK/81LL = L340-15/17IRH Gaming (2019).
                ["82EY", "82S9", "82UJ", "82SA", "82SB", "82UK", "82SC", "82SD", "82SE", "82SF",
                 "83C8", "83C9", "83CA", "83CB", "83CC", "83CD",
                 "83Z0", "83Z1", "83Z2", "83Z3",
                 "81Y4", "81LK", "81LL"],
                ["IdeaPad Gaming", "IdeaPad Gaming 3", "IdeaPad Gaming 3i", "IdeaPad Gaming 3 15", "IdeaPad Gaming 3 16",
                 "IdeaPad Gaming 3 15IAH7", "IdeaPad Gaming 3 16IAH7",
                 "拯救者 IdeaPad Gaming", "小新 Gaming"]),
            // Non-gaming consumer lines: basic profile (optimization only).
            LenovoBasicPack(
                "lenovo-ideapad",
                "Lenovo IdeaPad",
                ["IdeaPad"],
                ["IdeaPad"],
                [],
                ["IdeaPad", "IdeaPad Slim", "IdeaPad Flex", "IdeaPad 1", "IdeaPad 3", "IdeaPad 5", "IdeaPad Pro"]),
            LenovoBasicPack(
                "lenovo-thinkbook",
                "Lenovo ThinkBook",
                ["ThinkBook"],
                ["ThinkBook"],
                [],
                ["ThinkBook", "ThinkBook 14", "ThinkBook 16", "ThinkBook Plus"]),
            LenovoBasicPack(
                "lenovo-yoga",
                "Lenovo YOGA",
                ["YOGA"],
                [],
                [],
                ["YOGA", "Yoga", "Yoga Pro", "Yoga Slim", "Yoga Book", "Yoga 7", "Yoga 9"]),
            LenovoBasicPack(
                "lenovo-legion-desktop-basic",
                "Lenovo Legion Desktop Basic",
                ["Legion"],
                [],
                [],
                ["Legion T", "Legion Tower", "Legion C", "Legion Desktop"]),
            LenovoHardwarePack(
                "lenovo-legacy-limited",
                "Lenovo Legacy Limited",
                ["Legion"],
                ["18IAX", "17IR", "15IR", "15IC", "15IK", "G5000", "R9000", "R7000", "Y9000", "Y7000"],
                // Pre-2020 machines (limited/compat support).
                // 2018: 81FV/81LB Y530-15ICH(-1060), 81HD/81HG Y730-15/17ICH,
                //       81FW Y7000, 81LC Y7000-1060, 81HC/81LD Y7000P, 81LE/81LF Y7000P-1060.
                // 2019: 81SX/81SY/81RJ Y540-15, 81Q4/81T3 Y540-17, 81Q6/81T2 Y545(PG0),
                //       81QV/81HE/81UF/81UH Y740-15, 81QW/81HH/81UG/81UJ Y740-17,
                //       81NS/81T0/81V4 Y7000, 81Q5/81T1 Y7000P(PG0), 81JA/81UK Y9000K(SE).
                ["81FV", "81LB", "81HD", "81HG", "81FW", "81LC", "81HC", "81LD", "81LE", "81LF",
                 "81SX", "81SY", "81RJ", "81Q4", "81T3", "81Q6", "81T2",
                 "81QV", "81HE", "81UF", "81UH", "81QW", "81HH", "81UG", "81UJ",
                 "81NS", "81T0", "81V4", "81Q5", "81T1", "81JA", "81UK"],
                ["Legion", "Y530", "Y730", "Y540", "Y545", "Y740", "Y7000 2018", "Y7000 2019", "Y7000P 2018", "Y7000P 2019", "Y9000K 2019"]),
            LenovoBasicPack(
                "lenovo-thinkpad-basic",
                "Lenovo ThinkPad Basic",
                ["ThinkPad"],
                [],
                [],
                ["ThinkPad", "ThinkPad P", "ThinkPad T", "ThinkPad X", "ThinkPad E", "ThinkPad L", "ThinkPad Z",
                 "ThinkPad X1", "ThinkPad X1 Carbon", "ThinkPad X1 Yoga", "ThinkPad T14", "ThinkPad T16",
                 "ThinkPad P14s", "ThinkPad P16", "ThinkPad E14", "ThinkPad E16", "ThinkPad L14", "ThinkPad L16"]),
            LenovoBasicPack(
                "lenovo-thinkcentre-basic",
                "Lenovo ThinkCentre Basic",
                ["ThinkCentre"],
                [],
                [],
                ["ThinkCentre", "ThinkCentre M", "ThinkCentre Neo"]),
            LenovoBasicPack(
                "lenovo-thinkstation-basic",
                "Lenovo ThinkStation Basic",
                ["ThinkStation"],
                [],
                [],
                ["ThinkStation", "ThinkStation P"]),
            LenovoBasicPack(
                "lenovo-ideacentre-basic",
                "Lenovo IdeaCentre Basic",
                ["IdeaCentre"],
                [],
                [],
                ["IdeaCentre", "Yoga AIO"]),
            LenovoBasicPack(
                "lenovo-xiaoxin-basic",
                "Lenovo XiaoXin Basic",
                ["XiaoXin"],
                [],
                [],
                ["XiaoXin", "Xiaoxin", "小新"]),
            LenovoBasicPack(
                "lenovo-y-series-legacy",
                "Lenovo Y Series Legacy",
                ["IdeaPad", "Legion"],
                ["Y520", "Y530", "Y540", "Y545", "Y700", "Y7000", "Y9000"],
                [],
                ["Lenovo Y", "Y Series"]),
            LenovoBasicPack(
                "lenovo-v-series-basic",
                "Lenovo V Series Basic",
                ["Lenovo V"],
                ["V14", "V15", "V17"],
                [],
                ["Lenovo V", "V14", "V15", "V17"]),
            LenovoBasicPack(
                "lenovo-slim-basic",
                "Lenovo Slim Basic",
                ["Lenovo Slim", "IdeaPad Slim"],
                [],
                [],
                ["Lenovo Slim", "IdeaPad Slim", "Slim 7", "Slim 5"]),
            BasicPack(
                "motorola-lenovo-basic",
                "Motorola Lenovo Basic",
                "MOTOROLA",
                [],
                ["Motorola"],
                [],
                [],
                ["Legion", "ThinkPhone", "Moto"]),
            BasicPack(
                "asus-basic",
                "ASUS Basic",
                "ASUSTeK COMPUTER INC.",
                ["ASUS", "ASUSTeK COMPUTER INC", "ASUSTeK COMPUTER INCORPORATED", "ASUSTEK", "ROG", "Republic of Gamers", "TUF Gaming"],
                ["ASUS", "ROG", "TUF", "ProArt", "Zenbook", "Vivobook", "ExpertBook"],
                [],
                [],
                ["ROG", "ROG Strix", "ROG Flow", "ROG Zephyrus", "ROG Ally", "ROG Ally X", "TUF", "TUF Gaming", "Zephyrus", "Strix", "VivoBook", "Vivobook", "Zenbook", "ProArt", "ExpertBook", "Chromebook", "Chromebook Plus"])
                with
                {
                    // First non-Lenovo hardware provider: ATKACPI performance modes +
                    // ATK/LHM sensors. "lenovo-hardware-controls" is the generic
                    // hardware gate id (kept for compatibility). Fan curves, GPU
                    // overclock and keyboard backlight stay hidden for now.
                    EnabledFeatures = ["system-optimization", "language", "theme", "updates", "logs", "lenovo-hardware-controls", "sensors", "power-modes"],
                    HiddenFeatures = ["god-mode", "gpu-overclock", "fan-curve", "keyboard-backlight"],
                },
            BasicPack(
                "dell-basic",
                "Dell Basic",
                "Dell Inc.",
                ["Dell", "Dell Computer Corporation", "Alienware"],
                ["Dell", "Alienware", "Inspiron", "XPS", "Precision", "G Series", "Latitude", "OptiPlex"],
                [],
                [],
                ["Alienware", "Alienware m", "Alienware x", "XPS", "Inspiron", "Precision", "Latitude", "Dell G", "G15", "G16", "OptiPlex", "Vostro", "Chromebook", "Chromebook Plus"])
                with
                {
                    // Alienware AWCC (WMAX) provider: thermal profiles + sensors on
                    // Alienware / Dell G models; other Dell machines self-disable
                    // to the generic path.
                    EnabledFeatures = ["system-optimization", "language", "theme", "updates", "logs", "lenovo-hardware-controls", "sensors", "power-modes"],
                    HiddenFeatures = ["god-mode", "gpu-overclock", "fan-curve", "keyboard-backlight"],
                },
            BasicPack(
                "hp-basic",
                "HP Basic",
                "HP",
                ["HP Inc.", "Hewlett-Packard", "Hewlett-Packard Company", "Hewlett Packard", "OMEN", "Victus"],
                ["HP", "OMEN", "Victus", "Pavilion", "Envy", "EliteBook", "ProBook", "ZBook"],
                [],
                [],
                ["OMEN", "Omen Max", "OMEN Transcend", "Victus", "Pavilion", "Envy", "EliteBook", "ProBook", "ZBook", "Spectre", "Dragonfly", "Chromebook", "Chromebook Plus"])
                with
                {
                    // HP WMI BIOS provider (OMEN/Victus): performance modes + sensors.
                    // Fan curves, GPU OC and keyboard backlight stay hidden for now.
                    EnabledFeatures = ["system-optimization", "language", "theme", "updates", "logs", "lenovo-hardware-controls", "sensors", "power-modes"],
                    HiddenFeatures = ["god-mode", "gpu-overclock", "fan-curve", "keyboard-backlight"],
                },
            BasicPack(
                "acer-basic",
                "Acer Basic",
                "Acer",
                ["Acer Incorporated", "Acer Inc.", "Predator", "Nitro"],
                ["Acer", "Predator", "Nitro", "Swift", "Aspire", "TravelMate"],
                [],
                [],
                ["Predator", "Predator Helios", "Predator Triton", "Nitro", "Swift", "Aspire", "TravelMate", "ConceptD", "Extensa", "Spin", "Chromebook", "Chromebook Plus"])
                with
                {
                    // Acer WMID Gaming provider: thermal profiles + sensors on
                    // Predator/Nitro; other Acer lines self-disable to generic.
                    EnabledFeatures = ["system-optimization", "language", "theme", "updates", "logs", "lenovo-hardware-controls", "sensors", "power-modes"],
                    HiddenFeatures = ["god-mode", "gpu-overclock", "fan-curve", "keyboard-backlight"],
                },
            BasicPack(
                "msi-basic",
                "MSI Basic",
                "Micro-Star International Co., Ltd.",
                ["MSI", "Micro-Star International", "MICRO-STAR INTERNATIONAL CO., LTD"],
                ["MSI"],
                [],
                [],
                ["MSI", "Raider", "Stealth", "Vector", "Katana", "Cyborg", "Creator", "Prestige", "Modern", "Summit", "Crosshair", "Pulse", "Sword", "Thin"])
                with
                {
                    // MSI EC provider (PawnIO-backed EC channel): shift modes +
                    // sensors. Fan curves, GPU OC and keyboard backlight stay
                    // hidden for now.
                    EnabledFeatures = ["system-optimization", "language", "theme", "updates", "logs", "lenovo-hardware-controls", "sensors", "power-modes"],
                    HiddenFeatures = ["god-mode", "gpu-overclock", "fan-curve", "keyboard-backlight"],
                },
            BasicPack(
                "microsoft-surface-basic",
                "Microsoft Surface Basic",
                "Microsoft Corporation",
                ["Microsoft"],
                ["Surface"],
                [],
                [],
                ["Surface Laptop", "Surface Pro", "Surface Book", "Surface Studio", "Surface Go"]),
            BasicPack(
                "gigabyte-basic",
                "GIGABYTE Basic",
                "GIGABYTE",
                ["Gigabyte Technology Co., Ltd.", "Gigabyte Technology Co., Ltd", "AORUS"],
                ["GIGABYTE", "AORUS", "AERO"],
                [],
                [],
                ["AORUS", "AERO", "GIGABYTE G"])
                with
                {
                    // Gigabyte GB_WMIACPI provider, phase 1: sensors only. No
                    // platform power profile exists on this vendor (fan modes /
                    // GPU boost need raw WMBD writes with unproven semantics).
                    EnabledFeatures = ["system-optimization", "language", "theme", "updates", "logs", "lenovo-hardware-controls", "sensors"],
                    HiddenFeatures = ["power-modes", "god-mode", "gpu-overclock", "fan-curve", "keyboard-backlight"],
                },
            BasicPack(
                "razer-basic",
                "Razer Basic",
                "Razer",
                ["Razer Inc.", "Razer Inc"],
                ["Razer Blade"],
                [],
                [],
                ["Blade", "Razer Book"])
                with
                {
                    // Razer EC-over-HID provider: performance modes + fan reads.
                    // Fan curves, GPU OC and keyboard backlight stay hidden for now.
                    EnabledFeatures = ["system-optimization", "language", "theme", "updates", "logs", "lenovo-hardware-controls", "sensors", "power-modes"],
                    HiddenFeatures = ["god-mode", "gpu-overclock", "fan-curve", "keyboard-backlight"],
                },
            BasicPack(
                "samsung-basic",
                "Samsung Basic",
                "SAMSUNG ELECTRONICS CO., LTD.",
                ["Samsung", "Samsung Electronics", "SAMSUNG ELECTRONICS CO., LTD"],
                ["Samsung", "Galaxy Book"],
                [],
                [],
                ["Galaxy Book", "Notebook 9", "Galaxy Chromebook", "Chromebook", "Chromebook Plus"]),
            BasicPack(
                "google-chromebook-basic",
                "Google Chromebook Basic",
                "Google",
                ["Google LLC", "Google Inc.", "Google Inc"],
                ["Google", "Chromebook", "Pixelbook"],
                [],
                [],
                ["Pixelbook", "Pixel Slate", "Chromebook", "Chromebook Plus"]),
            BasicPack(
                "apple-basic",
                "Apple Basic",
                "Apple Inc.",
                ["Apple", "Apple Computer, Inc."],
                ["Mac", "MacBook", "iMac"],
                [],
                [],
                ["MacBook", "MacBook Pro", "MacBook Air", "iMac", "Mac mini", "Mac Studio", "Mac Pro"]),
            BasicPack(
                "huawei-basic",
                "HUAWEI Basic",
                "HUAWEI",
                ["Huawei", "Huawei Technologies", "Huawei Technologies Co., Ltd.", "Huawei Technologies Co., Ltd"],
                ["HUAWEI", "MateBook"],
                [],
                [],
                ["MateBook", "MateBook X", "MateBook D", "MateBook E", "MateBook GT", "Qingyun"]),
            BasicPack(
                "xiaomi-basic",
                "Xiaomi Basic",
                "Xiaomi",
                ["Xiaomi Inc.", "Xiaomi Corporation", "Redmi", "TIMI"],
                ["Xiaomi", "RedmiBook", "Mi Notebook"],
                [],
                [],
                ["Mi Notebook", "RedmiBook", "Redmi G", "Xiaomi Book", "Xiaomi Book Pro", "Book Pro"]),
            BasicPack(
                "realme-basic",
                "realme Basic",
                "realme",
                ["realme Chongqing MobileTelecommunications Corp., Ltd.", "realme"],
                ["realme", "realme Book"],
                [],
                [],
                ["realme Book"]),
            BasicPack(
                "infinix-basic",
                "Infinix Basic",
                "INFINIX",
                ["Infinix Mobility Limited", "Infinix"],
                ["Infinix", "INBook"],
                [],
                [],
                ["INBook", "Inbook"]),
            BasicPack(
                "honor-basic",
                "HONOR Basic",
                "HONOR",
                ["Honor Device Co., Ltd.", "Honor Device Co., Ltd"],
                ["HONOR", "MagicBook"],
                [],
                [],
                ["MagicBook"]),
            BasicPack(
                "lg-basic",
                "LG Basic",
                "LG Electronics",
                ["LG Electronics Inc.", "LG Electronics Inc", "LG"],
                ["LG gram", "LG"],
                [],
                [],
                ["gram", "UltraPC"]),
            BasicPack(
                "framework-basic",
                "Framework Basic",
                "Framework",
                ["Framework Computer Inc.", "Framework Computer"],
                ["Framework Laptop"],
                [],
                [],
                ["Framework Laptop"]),
            BasicPack(
                "panasonic-basic",
                "Panasonic Basic",
                "Panasonic",
                ["Panasonic Corporation"],
                ["Panasonic", "TOUGHBOOK", "Lets note"],
                [],
                [],
                ["TOUGHBOOK", "Let's note", "Lets note"]),
            BasicPack(
                "dynabook-basic",
                "Dynabook Basic",
                "Dynabook Inc.",
                ["Dynabook", "TOSHIBA", "TOSHIBA CORPORATION"],
                ["Dynabook", "Toshiba"],
                [],
                [],
                ["Portégé", "Portege", "Tecra", "Satellite"]),
            BasicPack(
                "nec-lavie-basic",
                "NEC LAVIE Basic",
                "NEC",
                ["NEC Personal Computers, Ltd.", "NEC Personal Computers", "NEC Corporation", "LAVIE"],
                ["NEC", "LAVIE", "VersaPro"],
                [],
                [],
                ["LAVIE", "LaVie", "VersaPro"]),
            BasicPack(
                "sharp-basic",
                "Sharp Basic",
                "SHARP",
                ["Sharp Corporation", "Sharp"],
                ["Sharp", "Mebius", "Dynabook"],
                [],
                [],
                ["Mebius", "Dynabook", "Chromebook"]),
            BasicPack(
                "fujitsu-basic",
                "Fujitsu Basic",
                "FUJITSU",
                ["FUJITSU CLIENT COMPUTING LIMITED", "Fujitsu Client Computing Limited"],
                ["Fujitsu", "LIFEBOOK"],
                [],
                [],
                ["LIFEBOOK", "CELSIUS"]),
            BasicPack(
                "vaio-basic",
                "VAIO Basic",
                "VAIO Corporation",
                ["VAIO"],
                ["VAIO"],
                [],
                [],
                ["VAIO"]),
            BasicPack(
                "gateway-basic",
                "Gateway Basic",
                "Gateway",
                ["Gateway Inc.", "Acer Gateway"],
                ["Gateway"],
                [],
                [],
                ["Gateway"]),
            BasicPack(
                "chuwi-basic",
                "CHUWI Basic",
                "CHUWI",
                ["Chuwi Innovation And Technology", "CHUWI Innovation Limited"],
                ["CHUWI"],
                [],
                [],
                ["HeroBook", "CoreBook", "MiniBook", "GemiBook", "FreeBook"]),
            BasicPack(
                "teclast-basic",
                "TECLAST Basic",
                "TECLAST",
                ["Teclast", "Guangzhou Shangke Information Technology"],
                ["TECLAST"],
                [],
                [],
                ["F15", "F16", "F7", "X6"]),
            BasicPack(
                "jumper-basic",
                "Jumper Basic",
                "Jumper",
                ["Jumper Computer", "Jumper Tech"],
                ["Jumper"],
                [],
                [],
                ["EZbook", "EZpad"]),
            BasicPack(
                "medion-basic",
                "MEDION Basic",
                "MEDION",
                ["MEDION AG", "ERAZER"],
                ["MEDION", "ERAZER"],
                [],
                [],
                ["ERAZER", "AKOYA"]),
            BasicPack(
                "xmg-schenker-basic",
                "XMG/SCHENKER Basic",
                "SCHENKER",
                ["Schenker Technologies GmbH", "XMG", "TUXEDO"],
                ["XMG", "SCHENKER", "TUXEDO"],
                [],
                [],
                ["XMG", "SCHENKER", "TUXEDO", "InfinityBook", "Stellaris", "Pulse", "Polaris", "Aura"]),
            BasicPack(
                "hasee-basic",
                "Hasee Basic",
                "HASEE",
                ["Hasee", "Hasee Computer"],
                ["Hasee"],
                [],
                [],
                ["Hasee", "ZhanShen", "Zhan Shen"])
                with
                {
                    // Hasee (Clevo / Tongfang ODM): EC power modes + sensors.
                    EnabledFeatures = ["system-optimization", "language", "theme", "updates", "logs", "lenovo-hardware-controls", "sensors", "power-modes"],
                    HiddenFeatures = ["god-mode", "gpu-overclock", "fan-curve", "keyboard-backlight"],
                },
            BasicPack(
                "thunderobot-basic",
                "THUNDEROBOT Basic",
                "THUNDEROBOT",
                ["Thunderobot", "Raytheon"],
                ["THUNDEROBOT", "Thunderobot"],
                [],
                [],
                ["Thunderobot", "911", "Zero", "Black Warrior"])
                with
                {
                    // Thunderobot (Tongfang / Clevo ODM): EC power modes + sensors.
                    EnabledFeatures = ["system-optimization", "language", "theme", "updates", "logs", "lenovo-hardware-controls", "sensors", "power-modes"],
                    HiddenFeatures = ["god-mode", "gpu-overclock", "fan-curve", "keyboard-backlight"],
                },
            BasicPack(
                "machenike-basic",
                "MACHENIKE Basic",
                "MACHENIKE",
                ["Machenike"],
                ["MACHENIKE", "Machenike"],
                [],
                [],
                ["MACHENIKE", "Machenike", "T58", "F117", "L16"])
                with
                {
                    // Machenike (Tongfang / Clevo ODM): EC power modes + sensors.
                    EnabledFeatures = ["system-optimization", "language", "theme", "updates", "logs", "lenovo-hardware-controls", "sensors", "power-modes"],
                    HiddenFeatures = ["god-mode", "gpu-overclock", "fan-curve", "keyboard-backlight"],
                },
            BasicPack(
                "colorful-basic",
                "COLORFUL Basic",
                "COLORFUL",
                ["Colorful Technology And Development Co., Ltd.", "Colorful"],
                ["COLORFUL", "Colorful"],
                [],
                [],
                ["COLORFUL", "Evol", "X15", "MEOW"])
                with
                {
                    // Colorful (Clevo / Tongfang ODM): EC power modes + sensors.
                    EnabledFeatures = ["system-optimization", "language", "theme", "updates", "logs", "lenovo-hardware-controls", "sensors", "power-modes"],
                    HiddenFeatures = ["god-mode", "gpu-overclock", "fan-curve", "keyboard-backlight"],
                },
            BasicPack(
                "maibenben-basic",
                "MAIBENBEN Basic",
                "MAIBENBEN",
                ["Maibenben", "MaiBenBen"],
                ["MAIBENBEN", "Maibenben"],
                [],
                [],
                ["Maibenben", "MaiBook", "Xiaomai"]),
            BasicPack(
                "mechrevo-basic",
                "MECHREVO Basic",
                "MECHREVO",
                ["Mechanical Revolution", "MECHREVO INC.", "Tongfang", "THTF", "Tsinghua Tongfang"],
                ["MECHREVO", "Mechanical Revolution"],
                [],
                [],
                ["MECHREVO", "Mechanical Revolution", "Jiaolong", "Kuangshi", "Code", "Unbounded", "F1"])
                with
                {
                    // Tongfang / MECHREVO EC provider: performance modes (Office/Gaming/Turbo) +
                    // EC sensors (temps + fan tachometers).
                    EnabledFeatures = ["system-optimization", "language", "theme", "updates", "logs", "lenovo-hardware-controls", "sensors", "power-modes"],
                    HiddenFeatures = ["god-mode", "gpu-overclock", "fan-curve", "keyboard-backlight"],
                },
            BasicPack(
                "valve-handheld-basic",
                "Valve Handheld Basic",
                "Valve",
                ["Valve Corporation"],
                ["Steam Deck"],
                [],
                [],
                ["Steam Deck"]),
            BasicPack(
                "gpd-handheld-basic",
                "GPD Handheld Basic",
                "GPD",
                ["GamePad Digital", "Shenzhen GPD Technology Co., Ltd."],
                ["GPD"],
                [],
                [],
                ["GPD WIN", "GPD Win", "Win Max", "Win Mini", "Pocket", "Duo"]),
            BasicPack(
                "ayaneo-handheld-basic",
                "AYANEO Handheld Basic",
                "AYANEO",
                ["AYANEO", "AOKZOE", "Ayn Technologies", "AYN"],
                ["AYANEO", "AOKZOE", "AYN"],
                [],
                [],
                ["AYANEO", "AOKZOE", "Loki", "NEXT", "Air Plus", "Odin", "Odin2"]),
            BasicPack(
                "anbernic-handheld-basic",
                "Anbernic Handheld Basic",
                "Anbernic",
                ["ANBERNIC", "Shenzhen Yangliming Electronic Technology"],
                ["Anbernic", "RG"],
                [],
                [],
                ["Anbernic", "RG35", "RG40", "RG505", "RG552", "RG556", "RG Arc", "Win600"]),
            BasicPack(
                "retroid-handheld-basic",
                "Retroid Handheld Basic",
                "Retroid",
                ["RETROID", "Moorechip Technologies", "GoRetroid"],
                ["Retroid", "Pocket"],
                [],
                [],
                ["Retroid", "Retroid Pocket", "Pocket 4", "Pocket 5"]),
            BasicPack(
                "orange-pi-handheld-basic",
                "Orange Pi Handheld Basic",
                "Orange Pi",
                ["Shenzhen Xunlong Software", "Xunlong", "OrangePi", "Orange Pi"],
                ["Orange Pi"],
                [],
                [],
                ["Orange Pi", "OrangePi", "Orange Pi Neo"]),
            BasicPack(
                "one-netbook-handheld-basic",
                "ONE-NETBOOK Handheld Basic",
                "ONE-NETBOOK",
                ["One-Netbook", "ONE-NETBOOK Technology", "ONEXPLAYER", "OneXPlayer"],
                ["ONE-NETBOOK", "ONEXPLAYER"],
                [],
                [],
                ["OneXPlayer", "ONEXPLAYER", "One-Netbook", "OneMix", "OneGx"]),
            BasicPack(
                "minisforum-basic",
                "MINISFORUM Basic",
                "MINISFORUM",
                ["Micro Computer (HK) Tech Limited", "Minisforum"],
                ["MINISFORUM"],
                [],
                [],
                ["MINISFORUM", "UM", "HX", "Venus Series"]),
            BasicPack(
                "beelink-basic",
                "Beelink Basic",
                "Beelink",
                ["AZW", "Shenzhen AZW Technology Co., Ltd.", "Beelink"],
                ["Beelink"],
                [],
                [],
                ["SER", "GTR", "EQ", "Beelink"]),
            BasicPack(
                "geekom-basic",
                "GEEKOM Basic",
                "GEEKOM",
                ["Geekom", "GEEKOM"],
                ["GEEKOM"],
                [],
                [],
                ["Mini IT", "MiniAir", "A7", "GT"]),
            BasicPack(
                "gmktec-basic",
                "GMKtec Basic",
                "GMKtec",
                ["GMK", "GMKtec", "Shenzhen GMKtec"],
                ["GMKtec", "NucBox"],
                [],
                [],
                ["NucBox", "EVO-X", "K8", "K6", "M7", "M5"]),
            BasicPack(
                "morefine-basic",
                "Morefine Basic",
                "Morefine",
                ["MORE FINE", "Shenzhen Morefine"],
                ["Morefine"],
                [],
                [],
                ["Morefine", "S500", "S600", "M600", "M9"]),
            BasicPack(
                "acemagic-basic",
                "ACEMAGIC Basic",
                "ACEMAGIC",
                ["Ace Magician", "ACEMAGICIAN", "ACEMAGIC", "KAMRUI"],
                ["ACEMAGIC", "ACEMAGICIAN", "KAMRUI"],
                [],
                [],
                ["ACEMAGIC", "Kron", "F5A", "Tank", "AM", "AD", "S1"]),
            BasicPack(
                "aoostar-basic",
                "AOOSTAR Basic",
                "AOOSTAR",
                ["AOOSTAR", "Aoostar"],
                ["AOOSTAR"],
                [],
                [],
                ["GEM", "GOD", "WTR", "MN", "N1", "R7 NAS"]),
            BasicPack(
                "regional-mini-pc-basic",
                "Regional Mini PC Basic",
                "TRIGKEY",
                ["TRIGKEY", "Trigkey", "BOSGAME", "Bosgame", "FIREBAT", "Firebat", "CHATREEY", "Chatreey", "SZBOX", "FEVM", "NiPoGi", "Nipogi", "PELADN", "KODLIX", "Topdon", "KTC"],
                ["Mini PC", "MiniPC"],
                [],
                [],
                []),
            BasicPack(
                "mele-basic",
                "MeLE Basic",
                "MeLE",
                ["Mele", "MeLE Technologies", "Shenzhen MeLE Digital Technology"],
                ["MeLE", "Quieter"],
                [],
                [],
                ["Quieter", "Overclock", "PCG", "Mini PC"]),
            BasicPack(
                "bmax-ninkear-basic",
                "BMAX/Ninkear Basic",
                "BMAX",
                ["BMAX", "Ninkear", "KUU", "N-one", "N-ONE", "MALLRACE"],
                ["BMAX", "Ninkear", "KUU", "N-one"],
                [],
                [],
                ["BMAX", "Ninkear", "KUU", "N-one", "N-one Nbook", "Mini PC", "Y13", "Y14", "X14", "X15"]),
            BasicPack(
                "zotac-basic",
                "ZOTAC Basic",
                "ZOTAC",
                ["ZOTAC International"],
                ["ZOTAC", "ZBOX"],
                [],
                [],
                ["ZBOX", "MAGNUS", "ZOTAC"]),
            BasicPack(
                "system76-basic",
                "System76 Basic",
                "System76",
                ["System76, Inc.", "System 76", "Notebook"],
                ["System76", "Pop!_OS", "Thelio"],
                [],
                [],
                ["Adder WS", "addw", "Bonobo WS", "bonw", "Darter Pro", "darp", "Galago Pro", "galp", "Gazelle", "gaze", "Kudu", "Lemur Pro", "lemp", "Meerkat", "meer", "Oryx Pro", "oryp", "Pangolin Pro", "panp", "Serval WS", "serw", "Thelio"]),
            BasicPack(
                "star-labs-basic",
                "Star Labs Basic",
                "Star Labs",
                ["Star Labs Systems", "StarLabs", "Star Labs Systems Ltd"],
                ["Star Labs", "StarBook", "StarLite"],
                [],
                [],
                ["StarFighter", "StarBook", "StarLite", "StarLite Mk", "Byte"]),
            BasicPack(
                "slimbook-basic",
                "Slimbook Basic",
                "SLIMBOOK",
                ["Slimbook", "Slimbook S.L.", "Slimbook SL"],
                ["Slimbook"],
                [],
                [],
                ["Elemental", "Excalibur", "EVO", "Executive", "Creative", "KDE Slimbook", "Fedora Slimbook", "Manjaro Slimbook", "Slimbook One", "Slimbook Zero"]),
            BasicPack(
                "clevo-tongfang-basic",
                "Clevo/Tongfang Basic",
                "CLEVO",
                ["Notebook", "Tongfang", "Eluktronics", "MECHREVO", "THUNDEROBOT", "Hasee", "SAGER"],
                ["Clevo", "Tongfang", "Barebone"],
                [],
                [],
                ["MECHREVO", "THUNDEROBOT", "Hasee", "SAGER", "Eluktronics", "Maingear", "Illegear", "Aftershock", "Hyperbook"])
                with
                {
                    // Clevo & Tongfang barebone provider: EC power modes + EC sensors.
                    EnabledFeatures = ["system-optimization", "language", "theme", "updates", "logs", "lenovo-hardware-controls", "sensors", "power-modes"],
                    HiddenFeatures = ["god-mode", "gpu-overclock", "fan-curve", "keyboard-backlight"],
                },
            BasicPack(
                "eluktronics-basic",
                "Eluktronics Basic",
                "Eluktronics",
                ["Eluktronics", "Eluktronics Inc."],
                ["Eluktronics"],
                [],
                [],
                ["Eluktronics", "RP", "MAX", "MECH", "Prometheus", "Hydroc"]),
            BasicPack(
                "maingear-basic",
                "MAINGEAR Basic",
                "MAINGEAR",
                ["Maingear", "MAINGEAR"],
                ["MAINGEAR"],
                [],
                [],
                ["MAINGEAR", "Vector", "ML", "MG"]),
            BasicPack(
                "monster-tulpar-basic",
                "Monster/Tulpar Basic",
                "Monster",
                ["Monster Notebook", "Monster Computer", "Tulpar"],
                ["Monster", "Tulpar"],
                [],
                [],
                ["Tulpar", "Abra", "Semruk", "Huma"]),
            BasicPack(
                "dream-machines-basic",
                "Dream Machines Basic",
                "Dream Machines",
                ["Dream Machines", "Dream Machines Sp. z o.o."],
                ["Dream Machines"],
                [],
                [],
                ["Dream Machines", "RG", "RT", "RX", "RS"]),
            BasicPack(
                "pcspecialist-basic",
                "PCSpecialist Basic",
                "PCSpecialist",
                ["PC Specialist", "PC Specialist Ltd", "PCSpecialist"],
                ["PCSpecialist", "PC Specialist"],
                [],
                [],
                ["Recoil", "Defiance", "Ionico", "Elimina", "Lafite"]),
            BasicPack(
                "eurocom-basic",
                "Eurocom Basic",
                "EUROCOM",
                ["Eurocom", "Eurocom Corporation"],
                ["EUROCOM"],
                [],
                [],
                ["Sky", "Nightsky", "Raptor", "Commander", "Panther"]),
            BasicPack(
                "origin-pc-basic",
                "Origin PC Basic",
                "Origin PC",
                ["ORIGIN PC", "OriginPC", "Corsair"],
                ["Origin PC", "OriginPC"],
                [],
                [],
                ["EON", "EON16", "EON17", "NS", "NT"]),
            BasicPack(
                "corsair-basic",
                "Corsair Basic",
                "Corsair",
                ["CORSAIR", "Corsair Memory, Inc.", "Corsair Components, Inc."],
                ["Corsair", "Voyager"],
                [],
                [],
                ["Voyager", "Corsair One", "Vengeance"]),
            BasicPack(
                "cyberpower-ibuypower-basic",
                "CyberPower/iBUYPOWER Basic",
                "CyberPowerPC",
                ["CyberPowerPC", "CyberPower Inc.", "iBUYPOWER", "iBUYPOWER Computer"],
                ["CyberPowerPC", "iBUYPOWER"],
                [],
                [],
                ["Tracer", "Gamer", "Slate", "Y60", "RDY"]),
            BasicPack(
                "casper-excalibur-basic",
                "Casper/Excalibur Basic",
                "Casper",
                ["Casper Bilgisayar", "Casper", "Excalibur"],
                ["Casper", "Excalibur"],
                [],
                [],
                ["Excalibur", "Nirvana"]),
            BasicPack(
                "nexstgo-avita-basic",
                "Nexstgo/Avita Basic",
                "Nexstgo",
                ["Nexstgo", "Nexstgo Company Limited", "Avita", "AVITA"],
                ["Nexstgo", "Avita"],
                [],
                [],
                ["AVITA", "Avita", "LIBER", "ADMIROR", "PURA"]),
            BasicPack(
                "positivo-basic",
                "Positivo Basic",
                "Positivo",
                ["Positivo Tecnologia", "Positivo Informatica", "Positivo"],
                ["Positivo"],
                [],
                [],
                ["Motion", "Vision", "Master", "Duo"]),
            BasicPack(
                "wortmann-terra-basic",
                "Wortmann/TERRA Basic",
                "Wortmann",
                ["Wortmann AG", "WORTMANN", "TERRA"],
                ["Wortmann", "TERRA"],
                [],
                [],
                ["TERRA", "Mobile", "Pad", "PC"]),
            BasicPack(
                "shinelon-basic",
                "Shinelon Basic",
                "Shinelon",
                ["Hasee", "Shinelon Computer"],
                ["Shinelon", "炫龙"],
                [],
                [],
                ["Shinelon", "炫龙"]),
            BasicPack(
                "dere-basic",
                "Dere Basic",
                "Dere",
                ["DERE", "戴睿"],
                ["Dere", "戴睿"],
                [],
                [],
                ["Dere", "戴睿"]),
            BasicPack(
                "tcl-basic",
                "TCL Basic",
                "TCL",
                ["TCL Technology", "TCL Communication"],
                ["TCL"],
                [],
                [],
                ["TCL Book", "TCL"]),
            BasicPack(
                "adata-xpg-basic",
                "ADATA/XPG Basic",
                "ADATA",
                ["ADATA Technology", "ADATA Technology Co., Ltd.", "XPG"],
                ["XPG"],
                [],
                [],
                ["XPG", "Xenia"]),
            BasicPack(
                "transsion-basic",
                "Tecno/Itel Basic",
                "TRANSSION",
                ["Tecno", "Itel", "TRANSSION HOLDINGS", "Transsion"],
                ["Tecno", "Itel"],
                [],
                [],
                ["Tecno", "Megabook", "Itel"]),
            BasicPack(
                "multilaser-basic",
                "Multilaser Basic",
                "Multilaser",
                ["Multilaser Industrial", "Multilaser Industrial S.A.", "MUL"],
                ["Multilaser"],
                [],
                [],
                ["Multilaser", "Ultra"]),
            BasicPack(
                "vestel-basic",
                "Vestel Basic",
                "Vestel",
                ["Vestel Elektronik", "Vestel Elektronik Sanayi"],
                ["Vestel"],
                [],
                [],
                ["Vestel"]),
            BasicPack(
                "axioo-basic",
                "Axioo Basic",
                "Axioo",
                ["Axioo International", "PT Axioo International"],
                ["Axioo"],
                [],
                [],
                ["Axioo", "Hype"]),
            BasicPack(
                "advan-basic",
                "Advan Basic",
                "Advan",
                ["Advan Digital", "PT Advan Digital"],
                ["Advan"],
                [],
                [],
                ["Advan"]),
            BasicPack(
                "universal-workstation-basic",
                "Universal Workstation Basic",
                "*",
                [],
                ["Workstation"],
                [],
                [],
                ["Workstation", "Precision", "ZBook", "ThinkStation", "ProArt Station", "Creator Workstation"]),
            BasicPack(
                "universal-motherboard-basic",
                "Universal Motherboard Basic",
                "*",
                ["ASUSTeK COMPUTER INC.", "ASUS", "Gigabyte Technology Co., Ltd.", "GIGABYTE", "Micro-Star International Co., Ltd.", "MSI", "ASRock", "ASRock Inc.", "BIOSTAR", "EVGA", "NZXT", "Supermicro", "Super Micro Computer, Inc.", "Intel Corporation"],
                ["Motherboard", "Custom PC"],
                [],
                [],
                ["To Be Filled By O.E.M.", "SYS-", "MS-7", "B650", "X670", "Z690", "Z790", "B760", "X570", "B550", "TRX40", "WRX80"]),
            BasicPack(
                "universal-desktop-basic",
                "Universal Desktop Basic",
                "*",
                [],
                ["Desktop", "Tower", "Mini PC", "All-in-One"],
                [],
                [],
                ["Desktop", "Tower", "Mini PC", "MiniPC", "AIO", "All-in-One", "All in One", "System Product Name"]),
            BasicPack(
                "universal-barebone-basic",
                "Universal Barebone Basic",
                "*",
                ["CLEVO", "Tongfang", "Notebook", "Hasee", "SAGER", "Eluktronics", "MECHREVO", "THUNDEROBOT"],
                ["Barebone"],
                [],
                [],
                ["Barebone", "To Be Filled By O.E.M.", "Default string"]),
            BasicPack(
                CatalogDeviceSupportProvider.GenericBasicPackId,
                "Universal PC Basic",
                "*",
                [],
                ["Generic PC", "Windows PC"],
                [],
                [],
                [])
        ]
    };

    public static readonly LenovoDeviceSupportProvider Instance = new();

    private LenovoDeviceSupportProvider()
        : base("universal", BuiltInCatalog)
    {
    }

    private static DevicePack LenovoHardwarePack(
        string id,
        string displayName,
        string[] families,
        string[] modelPrefixes,
        string[] machineTypes,
        string[] modelKeywords) =>
        new()
        {
            Id = id,
            DisplayName = displayName,
            Vendor = "LENOVO",
            Families = families,
            ModelPrefixes = modelPrefixes,
            MachineTypes = machineTypes,
            ModelKeywords = modelKeywords,
            EnabledFeatures = LenovoHardwareEnabledFeatures
        };

    private static DevicePack LenovoBasicPack(
        string id,
        string displayName,
        string[] families,
        string[] modelPrefixes,
        string[] machineTypes,
        string[] modelKeywords) =>
        BasicPack(id, displayName, "LENOVO", [], families, modelPrefixes, machineTypes, modelKeywords);

    private static DevicePack BasicPack(
        string id,
        string displayName,
        string vendor,
        string[] vendorAliases,
        string[] families,
        string[] modelPrefixes,
        string[] machineTypes,
        string[] modelKeywords) =>
        new()
        {
            Id = id,
            DisplayName = displayName,
            Vendor = vendor,
            VendorAliases = vendorAliases,
            Families = families,
            ModelPrefixes = modelPrefixes,
            MachineTypes = machineTypes,
            ModelKeywords = modelKeywords,
            EnabledFeatures = UniversalBasicEnabledFeatures,
            HiddenFeatures = UniversalBasicHiddenFeatures
        };
}
