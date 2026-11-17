using System.Collections.Generic;

namespace CouchCoop
{
    /// <summary>
    /// English / French UI strings, following the game's language (English by default).
    /// Only characters the game's bitmap fonts can draw (no Unicode arrows).
    /// </summary>
    internal static class L10n
    {
        private static readonly Dictionary<string, (string en, string fr)> Texts = new Dictionary<string, (string, string)>
        {
            ["title"] = ("Couch Co-op", "Coop locale"),
            ["tab.pads"] = ("Controllers", "Manettes"),
            ["tab.duo"] = ("Split screen", "Écran partagé"),
            ["tab.options"] = ("Options", "Options"),
            ["tab.optionsP2"] = ("P2 options", "Options J2"),

            ["pads.p1"] = ("P1", "J1"),
            ["pads.p2"] = ("P2", "J2"),
            ["pads.none"] = ("Unassigned", "Non assignée"),
            ["pads.empty"] = ("No controller detected", "Aucune manette détectée"),
            ["pads.taken1"] = ("P1 already has a controller", "J1 a déjà une manette"),
            ["pads.taken2"] = ("P2 already has a controller", "J2 a déjà une manette"),
            ["pads.saved"] = ("Controllers saved", "Manettes enregistrées"),
            ["pads.lost"] = ("Controller disconnected: plug it back in, or F8 to pick another", "Manette déconnectée : rebranche-la, ou F8 pour en choisir une autre"),
            ["pads.lostP2"] = ("Controller disconnected: plug it back in (P1 can pick another)", "Manette déconnectée : rebranche-la (J1 peut en choisir une autre)"),
            ["pads.back"] = ("Controller reconnected", "Manette reconnectée"),
            ["pads.p2note"] = ("The P2 controller only plays in the P2 window", "La manette J2 ne joue que dans la fenêtre J2"),

            ["duo.start"] = ("Start split screen", "Lancer l'écran partagé"),
            ["duo.restartP2"] = ("Restart the P2 window", "Relancer la fenêtre J2"),
            ["duo.layout"] = ("Half screen / full screen", "Moitié / plein écran"),
            ["duo.copy"] = ("Copy connection info", "Copier les infos de connexion"),
            ["duo.noServer"] = ("Dedicated server not found. Install Core Keeper Dedicated Server (Steam > Tools)", "Serveur dédié introuvable. Installe Core Keeper Dedicated Server (Steam > Outils)"),
            ["duo.started"] = ("Server starting... P2 opens when it is ready", "Serveur en démarrage... J2 s'ouvre dès qu'il est prêt"),
            ["duo.running"] = ("Split screen is already running", "L'écran partagé tourne déjà"),
            ["duo.isP2"] = ("This is the P2 window", "Tu es dans la fenêtre J2"),
            ["duo.copied"] = ("Copied. Paste it in the IP field", "Copié. Colle-le dans le champ IP"),
            ["duo.howto"] = ("Join Game > Join With IP > Join", "Rejoindre > Join With IP > Join"),

            ["duo.stop"] = ("Stop split screen (saves the world)", "Arrêter l'écran partagé (sauvegarde)"),
            ["duo.stopped"] = ("Split screen stopped. The server saves, then closes", "Écran partagé arrêté. Le serveur sauvegarde puis se ferme"),
            ["duo.stillStopping"] = ("The server is still saving and closing, try again in a few seconds", "Le serveur sauvegarde et se ferme encore, réessaie dans quelques secondes"),
            ["duo.notRunning"] = ("Split screen is not running", "L'écran partagé n'est pas lancé"),
            ["confirm.again"] = ("Press A again to confirm", "Appuie encore sur A pour confirmer"),
            ["confirm.press"] = ("A: confirm", "A : confirmer"),

            ["world.bringBack"] = ("Bring \"{0}\" back to solo", "Ramener \"{0}\" en solo"),
            ["world.backSame"] = ("replaces slot {0}", "remplace l'emplacement {0}"),
            ["world.backNew"] = ("new, slot {0}", "nouveau, emplacement {0}"),
            ["world.load"] = ("Load into the server", "Charger dans le serveur"),
            ["world.stopFirst"] = ("Stop split screen first (the server must be closed)", "Arrête d'abord l'écran partagé (le serveur doit être fermé)"),
            ["world.stopFirst.short"] = ("server running", "serveur lancé"),
            ["world.noServerWorld"] = ("The server has no world yet", "Le serveur n'a pas encore de monde"),
            ["world.noFreeSlot"] = ("No free world slot: delete a solo world first", "Aucun emplacement libre : supprime d'abord un monde solo"),
            ["world.noFreeSlot.short"] = ("no free slot", "aucun emplacement libre"),
            ["world.noSolo"] = ("No world in this slot", "Aucun monde à cet emplacement"),
            ["world.failed"] = ("Copy failed. The backup is in CouchCoop/Backups. See Player.log", "Échec de la copie. La sauvegarde est dans CouchCoop/Backups. Voir Player.log"),
            ["world.broughtBack"] = ("World copied to solo slot {0}. A backup was made", "Monde copié à l'emplacement solo {0}. Une sauvegarde a été faite"),
            ["world.loaded"] = ("\"{0}\" loaded into the server. A backup was made", "\"{0}\" chargé dans le serveur. Une sauvegarde a été faite"),

            ["status.ready"] = ("Server ready", "Serveur prêt"),
            ["status.starting"] = ("Server starting", "Serveur en démarrage"),
            ["status.stopped"] = ("Server stopped", "Serveur arrêté"),
            ["status.p2wait"] = ("Server starting, P2 waiting", "Serveur en démarrage, J2 attend"),
            ["status.waitInfo"] = ("Waiting for P1...", "En attente de J1..."),

            ["opt.layout"] = ("Layout", "Disposition"),
            ["opt.layout.stacked"] = ("P2 top, P1 bottom", "J2 en haut, J1 en bas"),
            ["opt.layout.side"] = ("P1 left, P2 right", "J1 à gauche, J2 à droite"),
            ["opt.fill"] = ("Fill each half (no black bars)", "Remplir chaque moitié"),
            ["opt.music"] = ("Mute P2 music", "Couper la musique de J2"),
            ["opt.autofill"] = ("Pre-fill Join With IP", "Pré-remplir Join With IP"),
            ["opt.autoopen"] = ("Open this menu for new controllers", "Ouvrir ce menu (nouvelle manette)"),
            ["opt.scale"] = ("Zoom (this window)", "Zoom (cette fenêtre)"),
            ["opt.fps"] = ("Frame rate cap", "Limite d'images/s"),
            ["opt.fps.game"] = ("Game setting", "Réglage du jeu"),
            ["opt.auto"] = ("Auto", "Auto"),
            ["opt.restart"] = ("Applies next time split screen starts", "S'applique au prochain lancement"),

            ["hint.tabs"] = ("LB/RB: tabs", "LB/RB : onglets"),
            ["hint.pads"] = ("Left/Right: move   A: save   B: unassign   Select: cancel", "Gauche/Droite : déplacer   A : valider   B : retirer   Select : annuler"),
            ["hint.list"] = ("Up/Down: choose   A: change   B: close", "Haut/Bas : choisir   A : changer   B : fermer"),
            ["card.p2.ready"] = ("Ready to join", "Prêt à rejoindre"),
            ["card.copy"] = ("copy", "copier"),
            ["card.hide"] = ("hide", "masquer"),
        };

        public static bool French
        {
            get
            {
                try { return (I2.Loc.LocalizationManager.CurrentLanguageCode ?? "").StartsWith("fr"); }
                catch { return false; }
            }
        }

        public static string T(string key)
        {
            if (!Texts.TryGetValue(key, out var t)) return key;
            return French ? t.fr : t.en;
        }
    }
}
