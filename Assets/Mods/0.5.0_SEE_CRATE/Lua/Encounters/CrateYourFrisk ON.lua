encountertext = "Poseur strikes a pose!"
nextwaves = {"thechoice"}
wavetimer = math.huge
arenasize = {155, 130}
unescape = true

enemies = {"wdspecial"}
enemypositions = {{0, 0}}

possible_attacks = {}

function EncounterStarting()
    Audio.Stop()
	SetAlMightyGlobal("CrateYourFrisk", true)
    State("DONE")
end

function Update() end
function EnemyDialogueStarting() end
function EnemyDialogueEnding() end
function DefenseEnding() end
function HandleSpare() end
function HandleItem(ItemID) end