using System;
using System.Collections.Generic;

using UnityEngine;
using UnityEngine.Localization;
using UnityEngine.Localization.Settings;

public enum RoatanLanguage
{
    English = 0,
    Spanish = 1,
    French = 2,
    German = 3
}

public static class RoatanLocalization
{
    private const string PrefLangKey = "RoatanDomino_Language";

    public static RoatanLanguage CurrentLanguage { get; private set; } = RoatanLanguage.English;
    public static event Action LanguageChanged;

    private static bool initialized;

    private static readonly Dictionary<string, string[]> Strings = new Dictionary<string, string[]>
    {
        // Order: [0] English (with Roatan Spanish flavor), [1] Spanish, [2] French, [3] German
        ["subtitle"] = new[]
        {
            "Caribbean Edition  •  Play Offline or Online",
            "Edición Caribe  •  Juega Sin Conexión o En Línea",
            "Édition Caraïbes  •  Jouez Hors Ligne ou En Ligne",
            "Karibik-Edition  •  Offline oder Online spielen"
        },
        ["choose_mode"] = new[] { "CHOOSE GAME MODE", "ELIGE MODO DE JUEGO", "CHOISISSEZ UN MODE", "SPIELMODUS WÄHLEN" },
        ["mode_cerrado_title"] = new[]
        {
            "Roatan Cerrado (Blocked)",
            "Roatán Cerrado (Trancado)",
            "Roatan Cerrado (Bloqué)",
            "Roatan Cerrado (Blockiert)"
        },
        ["mode_cerrado_desc"] = new[]
        {
            "4 Players  •  All 28 tiles dealt  •  No boneyard  •  Classic island strategy",
            "4 Jugadores  •  28 fichas repartidas  •  Sin pozo  •  Estrategia isleña",
            "4 Joueurs  •  28 dominos distribués  •  Sans pioche  •  Stratégie pure",
            "4 Spieler  •  Alle 28 Steine verteilt  •  Kein Stapel  •  Reine Strategie"
        },
        ["mode_draw_title"] = new[]
        {
            "Classic Draw Dominoes",
            "Dominó Clásico (Robar)",
            "Domino Classique (Pioche)",
            "Klassisches Domino (Ziehen)"
        },
        ["mode_draw_desc"] = new[]
        {
            "Draw from boneyard when blocked  •  Choose 1v1, 3P, or 4P",
            "Roba del pozo si no puedes jugar  •  Elige 1v1, 3J o 4J",
            "Piochez quand vous êtes bloqué  •  Choisissez 1v1, 3J ou 4J",
            "Vom Stapel ziehen wenn blockiert  •  Wähle 1v1, 3S oder 4S"
        },
        ["bot_difficulty"] = new[] { "BOT DIFFICULTY", "DIFICULTAD DE BOTS", "DIFFICULTÉ DES BOTS", "BOT-SCHWIERIGKEIT" },
        ["diff_beginner"] = new[] { "Beginner", "Principiante", "Débutant", "Anfänger" },
        ["diff_novice"] = new[] { "Novice", "Intermedio", "Intermédiaire", "Fortgeschritten" },
        ["diff_pro"] = new[] { "Pro", "Pro", "Pro", "Profi" },
        ["diff_beginner_desc"] = new[]
        {
            "Beginner: Friendly & relaxed moves — ideal for learning dominoes.",
            "Principiante: Jugadas relajadas — ideal para aprender.",
            "Débutant : Coups détendus — idéal pour apprendre.",
            "Anfänger: Entspannte Züge — ideal zum Lernen."
        },
        ["diff_novice_desc"] = new[]
        {
            "Novice: Drops heavy pips & doubles while keeping its hand open.",
            "Intermedio: Suelta fichas altas y dobles manteniendo su mano abierta.",
            "Intermédiaire : Joue les gros points et doubles tout en gardant des options.",
            "Fortgeschritten: Legt hohe Steine & Paschs ab und hält Optionen offen."
        },
        ["diff_pro_desc"] = new[]
        {
            "Pro: Counts played tiles, tracks your passes, and locks your suits.",
            "Pro: Cuenta fichas jugadas, sigue tus pasos y tranca tus pintas.",
            "Pro : Compte les dominos joués, suit vos passes et bloque vos suites.",
            "Profi: Zählt gespielte Steine, merkt sich deine Pässe und blockiert dich."
        },
        ["opponents"] = new[] { "OPPONENTS", "OPONENTES", "ADVERSAIRES", "GEGNER" },
        ["cerrado_opp_banner"] = new[]
        {
            "ROATAN CERRADO  •  4 PLAYERS (28 TILES, NO BONEYARD)",
            "ROATÁN CERRADO  •  4 JUGADORES (28 FICHAS, SIN POZO)",
            "ROATAN CERRADO  •  4 JOUEURS (28 DOMINOS, SANS PIOCHE)",
            "ROATAN CERRADO  •  4 SPIELER (28 STEINE, KEIN STAPEL)"
        },
        ["play_now"] = new[] { "PLAY NOW", "JUGAR AHORA", "JOUER", "JETZT SPIELEN" },
        ["sound_on"] = new[] { "Sound: ON", "Sonido: SÍ", "Son : OUI", "Ton: AN" },
        ["sound_off"] = new[] { "Sound: OFF", "Sonido: NO", "Son : NON", "Ton: AUS" },
        ["how_to_play"] = new[] { "How to Play", "Cómo Jugar", "Règles", "Anleitung" },
        ["online_mode"] = new[] { "Online Mode", "En Línea", "Mode En Ligne", "Online-Modus" },
        ["menu_back"] = new[] { "< Menu", "< Menú", "< Menu", "< Menü" },
        ["home_back"] = new[] { "< Home", "< Inicio", "< Accueil", "< Start" },
        ["fit"] = new[] { "Fit", "Ajustar", "Centrer", "Anpassen" },
        ["draw_tile"] = new[] { "Draw Tile ({0} left)", "Robar Ficha ({0})", "Piocher ({0})", "Ziehen ({0})" },
        ["pass_turn"] = new[] { "Pass Turn (Paso!)", "Pasar Turno (¡Paso!)", "Passer (Paso!)", "Passen (Paso!)" },
        ["blocked_reason"] = new[] { "Board Blocked (Trancado!)", "¡Mesa Trancada!", "Jeu Bloqué (Trancado!)", "Blockiert (Trancado!)" },
        ["domino_reason"] = new[] { "Domino! Hand Cleared", "¡Dominó! Mano Vacía", "Domino ! Main Vidée", "Domino! Hand Leer" },
        ["next_round"] = new[] { "Next Round ({0})", "Siguiente Ronda ({0})", "Manche Suivante ({0})", "Nächste Runde ({0})" },
        ["new_match"] = new[] { "New Match", "Nueva Partida", "Nouvelle Partie", "Neues Spiel" },
        ["main_menu"] = new[] { "Main Menu", "Menú Principal", "Menu Principal", "Hauptmenü" },
        ["got_it"] = new[] { "Got It!", "¡Entendido!", "Compris !", "Verstanden!" },
        ["online_title"] = new[] { "ONLINE MULTIPLAYER", "MULTIJUGADOR EN LÍNEA", "MULTIJOUEUR EN LIGNE", "ONLINE-MEHRSPIELER" },
        ["server_connected"] = new[] { "Server Connected", "Servidor Conectado", "Serveur Connecté", "Server Verbunden" },
        ["server_offline"] = new[] { "Server Unreachable — Check IP or Play Offline", "Servidor No Disponible — Verifica IP o Juega Sin Conexión", "Serveur Inaccessible — Vérifiez l'IP ou Jouez Hors Ligne", "Server Nicht Erreichbar — IP prüfen oder Offline spielen" },
        ["connecting"] = new[] { "Connecting to server...", "Conectando al servidor...", "Connexion au serveur...", "Verbinde mit Server..." },
        ["connect_retry"] = new[] { "Connect / Retry", "Conectar / Reintentar", "Connecter / Réessayer", "Verbinden / Neu" },
        ["sign_in"] = new[] { "Sign In", "Iniciar Sesión", "Connexion", "Anmelden" },
        ["create_account"] = new[] { "Create Account", "Crear Cuenta", "Créer un Compte", "Konto Erstellen" },
        ["sign_out"] = new[] { "Sign Out", "Salir", "Déconnexion", "Abmelden" },
        ["quick_ranked"] = new[] { "Quick Ranked Match", "Partida Clasificatoria", "Partie Classée Rapide", "Schnelles Ranglistenspiel" },
        ["create_lobby"] = new[] { "Create Lobby", "Crear Sala", "Créer un Salon", "Lobby Erstellen" },
        ["join_code"] = new[] { "Join Code", "Unirse con Código", "Code Salon", "Mit Code Beitreten" },
        ["refresh_lobbies"] = new[] { "Refresh Lobbies", "Actualizar Salas", "Actualiser", "Aktualisieren" },
        ["ready"] = new[] { "Ready", "Listo", "Prêt", "Bereit" },
        ["not_ready"] = new[] { "Not Ready", "No Listo", "Pas Prêt", "Nicht Bereit" },
        ["leave_lobby"] = new[] { "Leave Lobby", "Salir de Sala", "Quitter le Salon", "Lobby Verlassen" }
    };

    public static void Initialize()
    {
        if (initialized) return;
        initialized = true;

        int saved = PlayerPrefs.GetInt(PrefLangKey, (int)RoatanLanguage.English);
        if (saved < 0 || saved > 3) saved = 0;
        SetLanguage((RoatanLanguage)saved, save: false);
    }

    public static void SetLanguage(RoatanLanguage lang, bool save = true)
    {
        CurrentLanguage = lang;
        if (save)
        {
            PlayerPrefs.SetInt(PrefLangKey, (int)lang);
            PlayerPrefs.Save();
        }

        SyncUnityLocale(lang);
        LanguageChanged?.Invoke();
    }

    public static string CodeFor(RoatanLanguage lang)
    {
        switch (lang)
        {
            case RoatanLanguage.English: return "EN";
            case RoatanLanguage.Spanish: return "ES";
            case RoatanLanguage.French: return "FR";
            case RoatanLanguage.German: return "DE";
            default: return "EN";
        }
    }

    public static string T(string key)
    {
        Initialize();
        if (Strings.TryGetValue(key, out string[] row))
        {
            int idx = (int)CurrentLanguage;
            if (idx >= 0 && idx < row.Length)
            {
                return row[idx];
            }
            return row[0];
        }
        return key;
    }

    public static string Tf(string key, params object[] args)
    {
        return string.Format(T(key), args);
    }

    public static string DifficultyLabel(BotDifficulty diff)
    {
        switch (diff)
        {
            case BotDifficulty.Beginner: return T("diff_beginner");
            case BotDifficulty.Novice: return T("diff_novice");
            case BotDifficulty.Pro: return T("diff_pro");
            default: return diff.ToString();
        }
    }

    private static void SyncUnityLocale(RoatanLanguage lang)
    {
        string targetPrefix;
        switch (lang)
        {
            case RoatanLanguage.Spanish: targetPrefix = "es"; break;
            case RoatanLanguage.French: targetPrefix = "fr"; break;
            case RoatanLanguage.German: targetPrefix = "de"; break;
            default: targetPrefix = "en"; break;
        }

        try
        {
            var available = LocalizationSettings.AvailableLocales;
            if (available != null && available.Locales != null && available.Locales.Count > 0)
            {
                for (int i = 0; i < available.Locales.Count; i++)
                {
                    Locale loc = available.Locales[i];
                    if (loc != null && loc.Identifier.Code.StartsWith(targetPrefix, StringComparison.OrdinalIgnoreCase))
                    {
                        LocalizationSettings.SelectedLocale = loc;
                        MenuPrincipal.IdLanguageUsed = i;
                        return;
                    }
                }
            }
        }
        catch
        {
            // Safe fallback if LocalizationSettings has not finished initializing yet
        }
    }
}
