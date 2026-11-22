using System;

public static class RoatanQuickEmotes
{
    public static readonly string[] Phrases =
    {
        "Pose up!",
        "Pass!",
        "Game Bruk!",
        "Slam it!",
        "Six-Lov incoming!",
        "Padna!",
        "Key / Capicú!"
    };

    public static bool IsAllowedQuickEmote(string message)
    {
        if (string.IsNullOrWhiteSpace(message)) return false;
        string trimmed = message.Trim();

        if (string.Equals(trimmed, "Break!", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "Key!", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "Capicu!", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(trimmed, "Capicú!", StringComparison.OrdinalIgnoreCase))
        {
            return true;
        }

        for (int i = 0; i < Phrases.Length; i++)
        {
            if (string.Equals(trimmed, Phrases[i], StringComparison.OrdinalIgnoreCase))
            {
                return true;
            }
        }

        return false;
    }
}
