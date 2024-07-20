﻿using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.SceneManagement;

public class SelectOMatic : MonoBehaviour {
    private static int CurrentSelectedMod;
    private static List<DirectoryInfo> modDirs;
    private Dictionary<string, Sprite> bgs = new Dictionary<string, Sprite>();
    private bool animationDone = true;
    private float animationTimer;
    public EventSystem eventSystem;

    private static float modListScroll;         // Used to keep track of the position of the mod list specifically. Resets if you press escape
    private static float encounterListScroll;   // Used to keep track of the position of the encounter list. Resets if you press escape

    private float ExitButtonAlpha = 5f;         // Used to fade the "Exit" button in and out
    private float OptionsButtonAlpha = 5f;      // Used to fade the "Options" button in and out

    private static int selectedItem;            // Used to let users navigate the mod and encounter menus with the arrow keys!

    public GameObject encounterBox, devMod, content, retromodeWarning;
    public GameObject btnList,              btnBack,              btnNext,              btnExit,              btnOptions;
    public Text       ListText, ListShadow, BackText, BackShadow, NextText, NextShadow, ExitText, ExitShadow, OptionsText, OptionsShadow;
    public GameObject ModContainer,  ModBackground,     ModTitle,     ModTitleShadow,     EncounterCount,     EncounterCountShadow;
    public GameObject AnimContainer, AnimModBackground, AnimModTitle, AnimModTitleShadow, AnimEncounterCount, AnimEncounterCountShadow;

    // Use this for initialization
    private void Start() {
        Destroy(GameObject.Find("Player"));
        Destroy(GameObject.Find("Main Camera OW"));
        Destroy(GameObject.Find("Canvas OW"));
        Destroy(GameObject.Find("Canvas Two"));
        UnitaleUtil.firstErrorShown = false;

        // Load directory info
        DirectoryInfo di = new DirectoryInfo(Path.Combine(FileLoader.DataRoot, "Mods"));
        var modDirsTemp = di.GetDirectories();

        // Remove mods with 0 encounters and hidden mods from the list
        List<DirectoryInfo> purged = (from modDir in modDirsTemp
                                      let encPath = Path.Combine(FileLoader.DataRoot, "Mods/" + modDir.Name + "/Lua/Encounters")
                                      where new DirectoryInfo(encPath).Exists
                                      let hasEncounters = new DirectoryInfo(encPath).GetFiles("*.lua").Where(e => !e.Name.StartsWith("@")).Any()
                                      where hasEncounters && (modDir.Attributes & FileAttributes.Hidden) != FileAttributes.Hidden && !modDir.Name.StartsWith("@")
                                      select modDir).ToList();
        modDirs = purged;

        // Make sure that there is at least one playable mod present
        if (modDirs.Count == 0) {
            GlobalControls.modDev = false;
            UnitaleUtil.DisplayLuaError("loading", "<b>Your mod folder is empty!</b>\nYou need at least 1 playable mod to use the Mod Selector.\n\n"
                + "Remember:\n1. Mods whose names start with \"@\" do not count\n2. Folders without encounter files or with only encounters whose names start with \"@\" do not count");
            return;
        }

        modDirs.Sort((a, b) => string.Compare(a.Name, b.Name, StringComparison.OrdinalIgnoreCase));

        // Bind button functions
        btnBack.GetComponent<Button>().onClick.RemoveAllListeners();
        btnBack.GetComponent<Button>().onClick.AddListener(() => {
            eventSystem.SetSelectedGameObject(null);
            if (!animationDone) return;
            modFolderSelection();
            ScrollMods(-1);
        });
        btnNext.GetComponent<Button>().onClick.RemoveAllListeners();
        btnNext.GetComponent<Button>().onClick.AddListener(() => {
            eventSystem.SetSelectedGameObject(null);
            if (!animationDone) return;
            modFolderSelection();
            ScrollMods( 1);
        });

        // Give the mod list button a function
        btnList.GetComponent<Button>().onClick.RemoveAllListeners();
        btnList.GetComponent<Button>().onClick.AddListener(() => {
            eventSystem.SetSelectedGameObject(null);
            if (animationDone)
                modFolderMiniMenu();
        });
        // Grab the exit button, and give it some functions
        btnExit.GetComponent<Button>().onClick.RemoveAllListeners();
        btnExit.GetComponent<Button>().onClick.AddListener(() => {
            eventSystem.SetSelectedGameObject(null);
            SceneManager.LoadScene("Disclaimer");
            DiscordControls.StartTitle();
        });

        // Add devMod button functions
        if (GlobalControls.modDev) {
            btnOptions.GetComponent<Button>().onClick.RemoveAllListeners();
            btnOptions.GetComponent<Button>().onClick.AddListener(() => {
                eventSystem.SetSelectedGameObject(null);
                SceneManager.LoadScene("Options");
            });
        }

        // Crate Your Frisk initializer
        if (GlobalControls.crate) {
            //Exit button
            ExitText.text   = "← BYEE (RATIO'D)";
            ExitShadow.text = ExitText.text;

            //Options button
            OptionsText.text   = "OPSHUNZ (YUMMY) →";
            OptionsShadow.text = OptionsText.text;

            //Back button within scrolling list
            content.transform.Find("Back/Text").GetComponent<Text>().text = "← BCAK";

            //Mod list button
            ListText.gameObject.GetComponent<Text>().text   = "MDO LITS";
            ListShadow.gameObject.GetComponent<Text>().text = "MDO LITS";
        }

        retromodeWarning.SetActive(GlobalControls.retroMode);

        // This check will be true if we just exited out of an encounter
        // If that's the case, we want to open the encounter list so the user only has to click once to re enter
        modFolderSelection();
        if (StaticInits.ENCOUNTER != "") {
            //Check to see if there is more than one encounter in the mod just exited from
            DirectoryInfo di2 = new DirectoryInfo(Path.Combine(FileLoader.ModDataPath, "Lua/Encounters"));
            string[] encounters = di2.GetFiles("*.lua").Select(f => Path.GetFileNameWithoutExtension(f.Name)).Where(f => !f.StartsWith("@")).ToArray();

            if (encounters.Length > 1) {
                // Highlight the chosen encounter whenever the user exits the mod menu
                int temp = selectedItem;
                encounterSelection();
                selectedItem = temp;
                content.transform.GetChild(selectedItem).GetComponent<MenuButton>().StartAnimation(1);
            }

            // Move the scrolly bit to where it was when the player entered the encounter
            content.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, encounterListScroll);

            // Start the Exit button at half transparency
            ExitButtonAlpha                       = 0.5f;
            ExitText.GetComponent<Text>().color   = new Color(1f, 1f, 1f, 0.5f);
            ExitShadow.GetComponent<Text>().color = new Color(0f, 0f, 0f, 0.5f);

            // Start the Options button at half transparency
            if (GlobalControls.modDev) {
                OptionsButtonAlpha                       = 0.5f;
                OptionsText.GetComponent<Text>().color   = new Color(1f, 1f, 1f, 0.5f);
                OptionsShadow.GetComponent<Text>().color = new Color(0f, 0f, 0f, 0.5f);
            }

            // Reset it to let us accurately tell if the player just came here from the Disclaimer scene or the Battle scene
            StaticInits.ENCOUNTER = "";
            // Player is coming here from the Disclaimer scene
        } else {
            // When the player enters from the Disclaimer screen, reset stored scroll positions
            modListScroll       = 0.0f;
            encounterListScroll = 0.0f;
        }
    }

    // A special function used specifically for error handling
    // It re-generates the mod list, and selects the first mod
    // Used for cases where the player selects a mod or encounter that no longer exists
    private void HandleErrors() {
        Debug.LogWarning("Mod or Encounter not found! Resetting mod list...");
        CurrentSelectedMod = 0;
        bgs = new Dictionary<string, Sprite>();
        Start();
    }

    private IEnumerator LaunchMod() {
        // First: make sure the mod is still here and can be opened
        if (!new DirectoryInfo(Path.Combine(FileLoader.DataRoot, "Mods/" + modDirs[CurrentSelectedMod].Name + "/Lua/Encounters/")).Exists
         || !File.Exists(Path.Combine(FileLoader.DataRoot, "Mods/" + modDirs[CurrentSelectedMod].Name + "/Lua/Encounters/" + StaticInits.ENCOUNTER + ".lua"))) {
            HandleErrors();
            yield break;
        }

        // Dim the background to indicate loading
        ModBackground.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.1875f);

        // Store the current position of the scrolly bit
        encounterListScroll = content.GetComponent<RectTransform>().anchoredPosition.y;

        yield return new WaitForEndOfFrame();
        try {
            StaticInits.InitAll(StaticInits.MODFOLDER, true);
            if (UnitaleUtil.firstErrorShown)
                throw new Exception();
            Debug.Log("Loading " + StaticInits.ENCOUNTER);
            GlobalControls.isInFight = true;
            DiscordControls.StartBattle(modDirs[CurrentSelectedMod].Name, StaticInits.ENCOUNTER);
            SceneManager.LoadScene("Battle");
        } catch (Exception e) {
            ModBackground.GetComponent<Image>().color = new Color(1f, 1f, 1f, 0.25f);
            Debug.LogError("An error occured while loading a mod:\n" + e.Message + "\n\n" + e.StackTrace);
        }
    }

    // Shows a mod's "page".
    private void ShowMod(int id) {
        // Error handler
        // If current index is now out of range OR currently selected mod is not present:
        if (id < 0 || id > modDirs.Count - 1
            || !new DirectoryInfo(Path.Combine(FileLoader.DataRoot, "Mods/" + modDirs[id].Name + "/Lua/Encounters")).Exists
            ||  new DirectoryInfo(Path.Combine(FileLoader.DataRoot, "Mods/" + modDirs[id].Name + "/Lua/Encounters")).GetFiles("*.lua").Length == 0) {
            HandleErrors();
            return;
        }

        // Update currently selected mod folder
        StaticInits.MODFOLDER = modDirs[id].Name;

        // Make clicking the background go to the encounter select screen
        ModBackground.GetComponent<Button>().onClick.RemoveAllListeners();
        ModBackground.GetComponent<Button>().onClick.AddListener(() => {
            eventSystem.SetSelectedGameObject(null);
            if (animationDone) {
                encounterSelection();
                content.transform.GetChild(selectedItem).GetComponent<MenuButton>().StartAnimation(1);
            }
        });

        // Update the background
        var ImgComp = ModBackground.GetComponent<Image>();
        FileLoader.absoluteSanitizationDictionary.Clear();
        FileLoader.relativeSanitizationDictionary.Clear();
        // First, check if we already have this mod's background loaded in memory
        if (bgs.ContainsKey(modDirs[id].Name)) {
            ImgComp.sprite = bgs[modDirs[id].Name];
        } else {
            // if not, find it and store it
            try {
                Sprite thumbnail = SpriteUtil.FromFile("preview.png");
                ImgComp.sprite = thumbnail;
            } catch {
                try {
                    Sprite bg = SpriteUtil.FromFile("bg.png");
                    ImgComp.sprite = bg;
                } catch { ImgComp.sprite = SpriteUtil.FromFile("black.png"); }
            }
            bgs.Add(modDirs[id].Name, ImgComp.sprite);
        }

        // Get all encounters in the mod's Encounters folder
        DirectoryInfo di        = new DirectoryInfo(Path.Combine(FileLoader.ModDataPath, "Lua/Encounters"));
        string[] encounters = di.GetFiles("*.lua").Select(f => Path.GetFileNameWithoutExtension(f.Name)).Where(f => !f.StartsWith("@")).ToArray();

        // Update the text
        ModTitle.GetComponent<Text>().text = modDirs[id].Name;
        // Crate your frisk version
        if (GlobalControls.crate)
            ModTitle.GetComponent<Text>().text = Temmify.Convert(modDirs[id].Name, true);
        ModTitleShadow.GetComponent<Text>().text = ModTitle.GetComponent<Text>().text;

        // List # of encounters, or name of encounter if there is only one
        if (encounters.Length == 1) {
            EncounterCount.GetComponent<Text>().text = encounters[0];
            // crate your frisk version
            if (GlobalControls.crate)
                EncounterCount.GetComponent<Text>().text = Temmify.Convert(encounters[0],  true);

            // Make clicking the bg directly open the encounter
            ModBackground.GetComponent<Button>().onClick.RemoveAllListeners();
            ModBackground.GetComponent<Button>().onClick.AddListener(() => {
                eventSystem.SetSelectedGameObject(null);
                if (!animationDone) return;
                StaticInits.ENCOUNTER = encounters[0];
                StartCoroutine(LaunchMod());
            });
        } else {
            EncounterCount.GetComponent<Text>().text = "Has " + encounters.Length + " encounters";
            // crate your frisk version
            if (GlobalControls.crate)
                EncounterCount.GetComponent<Text>().text = "HSA " + encounters.Length + " ENCUOTNERS";
        }
        EncounterCountShadow.GetComponent<Text>().text = EncounterCount.GetComponent<Text>().text;

        // Update the color of the arrows
        if (id == 0 && modDirs.Count == 1)
            BackText.color = new Color(0.25f, 0.25f, 0.25f, 1f);
        else
            BackText.color = new Color(1f, 1f, 1f, 1f);
        if (id == modDirs.Count - 1 && modDirs.Count == 1)
            NextText.color = new Color(0.25f, 0.25f, 0.25f, 1f);
        else
            NextText.color = new Color(1f, 1f, 1f, 1f);
    }

    // Goes to the next or previous mod with a little scrolling animation.
    // -1 for left, 1 for right
    private void ScrollMods(int dir) {
        // First, determine if the next mod should be shown
        bool animate = modDirs.Count > 1;
        //if ((dir == -1 && CurrentSelectedMod > 0) || (dir == 1 && CurrentSelectedMod < modDirs.Count - 1)) {

        // If the new mod is being shown, start the animation!
        if (!animate) return;
        animationTimer = dir / 10f;
        animationDone  = false;

        // Enable the "ANIM" assets
        AnimContainer.SetActive(true);
        AnimContainer.transform.localPosition                 = new Vector2(0, 0);
        AnimModBackground       .GetComponent<Image>().sprite = ModBackground.GetComponent<Image>().sprite;
        AnimModTitleShadow      .GetComponent<Text>().text    = ModTitleShadow.GetComponent<Text>().text;
        AnimModTitle            .GetComponent<Text>().text    = ModTitle.GetComponent<Text>().text;
        AnimEncounterCountShadow.GetComponent<Text>().text    = EncounterCountShadow.GetComponent<Text>().text;
        AnimEncounterCount      .GetComponent<Text>().text    = EncounterCount.GetComponent<Text>().text;

        // Move all real assets to the side
        ModBackground.transform.Translate(640        * dir, 0, 0);
        ModTitleShadow.transform.Translate(640       * dir, 0, 0);
        ModTitle.transform.Translate(640             * dir, 0, 0);
        EncounterCountShadow.transform.Translate(640 * dir, 0, 0);
        EncounterCount.transform.Translate(640       * dir, 0, 0);

        // Actually choose the new mod
        CurrentSelectedMod = (CurrentSelectedMod + dir) % modDirs.Count;
        if (CurrentSelectedMod < 0) CurrentSelectedMod += modDirs.Count;

        ShowMod(CurrentSelectedMod);
    }

    // Used to animate scrolling left or right.
    private void Update() {
        // Animation updating section
        if (AnimContainer.activeSelf) {
            animationTimer = animationTimer > 0 ? Mathf.Floor(animationTimer + 1) : Mathf.Ceil (animationTimer - 1);

            int distance = (int)((20 - Mathf.Abs(animationTimer)) * 3.4 * -Mathf.Sign(animationTimer));

            AnimContainer.transform.Translate(distance, 0, 0);
            ModContainer.transform.Translate(distance, 0, 0);

            if (Mathf.Abs(animationTimer) == 20) {
                AnimContainer.SetActive(false);

                // Manual movement because I can't change the movement multiplier to a precise enough value
                ModContainer.transform.Translate((int)(2 * -Mathf.Sign(animationTimer)), 0, 0);

                animationTimer = 0;
                animationDone = true;
            }
        }

        // Prevent scrolling too far in the encounter box
        if (encounterBox.activeSelf) {
            if (content.GetComponent<RectTransform>().anchoredPosition.y < -200)
                content.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, -200);
            else if (content.GetComponent<RectTransform>().anchoredPosition.y > (content.transform.childCount - 1) * 30)
                content.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, (content.transform.childCount - 1) * 30);
        }

        // Detect hovering over the Exit button and handle fading
        if (ScreenResolution.mousePosition.x / ScreenResolution.displayedSize.x * 640 < 70 && Input.mousePosition.y / ScreenResolution.displayedSize.y * 480 > 450 && ExitButtonAlpha < 1f) {
            ExitButtonAlpha += 0.05f;
            ExitText.color   = new Color(1f, 1f, 1f, ExitButtonAlpha);
            ExitShadow.color = new Color(0f, 0f, 0f, ExitButtonAlpha);
        } else if (ExitButtonAlpha > 0.5f) {
            ExitButtonAlpha -= 0.05f;
            ExitText.color   = new Color(1f, 1f, 1f, ExitButtonAlpha);
            ExitShadow.color = new Color(0f, 0f, 0f, ExitButtonAlpha);
        }

        // Detect hovering over the Options button and handle fading
        if (GlobalControls.modDev) {
            if (ScreenResolution.mousePosition.x / ScreenResolution.displayedSize.x * 640 > 550 && Input.mousePosition.y / ScreenResolution.displayedSize.y * 480 > 450 && OptionsButtonAlpha < 1f) {
                OptionsButtonAlpha += 0.05f;
                OptionsText.color   = new Color(1f, 1f, 1f, OptionsButtonAlpha);
                OptionsShadow.color = new Color(0f, 0f, 0f, OptionsButtonAlpha);
            } else if (OptionsButtonAlpha > 0.5f) {
                OptionsButtonAlpha -= 0.05f;
                OptionsText.color   = new Color(1f, 1f, 1f, OptionsButtonAlpha);
                OptionsShadow.color = new Color(0f, 0f, 0f, OptionsButtonAlpha);
            }
        }

        // Controls:

        ////////////////// Main: ////////////////////////////////////
        //        Confirm: Start encounter (if mod has only one    //
        //                 encounter), or open encounter list      //
        //         Cancel: Return to Disclaimer screen             //
        //             Up: Open the mod list                       //
        //           Menu: Open the options menu                   //
        //           Left: Scroll left                             //
        //          Right: Scroll right                            //
        ////////////////// Encounter or Mod list: ///////////////////
        //        Confirm: Start an encounter, or select a mod     //
        //         Cancel: Exit                                    //
        //             Up: Move up                                 //
        //           Down: Move down                               //
        /////////////////////////////////////////////////////////////

        if (!encounterBox.activeSelf) {
            // Main controls
            if (animationDone) {
                // Move left
                if (GlobalControls.input.Left == ButtonState.PRESSED)       ScrollMods(-1);
                // Move right
                else if (GlobalControls.input.Right == ButtonState.PRESSED) ScrollMods(1);
                // Open the mod list
                else if (GlobalControls.input.Up == ButtonState.PRESSED) {
                    modFolderMiniMenu();
                    content.transform.GetChild(selectedItem).GetComponent<MenuButton>().StartAnimation(1);
                // Open the encounter list or start the encounter (if there is only one encounter)
                } else if (GlobalControls.input.Confirm == ButtonState.PRESSED)
                    ModBackground.GetComponent<Button>().onClick.Invoke();
            }

            // Access the Options menu
            if (GlobalControls.input.Menu == ButtonState.PRESSED)
                btnOptions.GetComponent<Button>().onClick.Invoke();
            // Return to the Disclaimer screen
            if (GlobalControls.input.Cancel == ButtonState.PRESSED)
                btnExit.GetComponent<Button>().onClick.Invoke();
        } else {
            // Encounter or Mod List controls
            if (GlobalControls.input.Up == ButtonState.PRESSED || GlobalControls.input.Down == ButtonState.PRESSED) {
                // Store previous value of selectedItem
                int previousSelectedItem = selectedItem;

                // Move up or down the list
                selectedItem += GlobalControls.input.Up == ButtonState.PRESSED ? -1 : 1;

                // Keep the selector in-bounds
                if (selectedItem < 0)                                     selectedItem = content.transform.childCount - 1;
                else if (selectedItem > content.transform.childCount - 1) selectedItem = 0;

                // Animate the old button
                GameObject previousButton = content.transform.GetChild(previousSelectedItem).gameObject;
                previousButton.GetComponent<MenuButton>().StartAnimation(-1);

                // Animate the new button
                GameObject newButton = content.transform.GetChild(selectedItem).gameObject;
                newButton.GetComponent<MenuButton>().StartAnimation(1);

                // Scroll to the newly chosen button if it is hidden!
                float buttonTopEdge    = -newButton.GetComponent<RectTransform>().anchoredPosition.y + 100;
                float buttonBottomEdge = -newButton.GetComponent<RectTransform>().anchoredPosition.y + 100 + 30;

                float topEdge    = content.GetComponent<RectTransform>().anchoredPosition.y;
                float bottomEdge = content.GetComponent<RectTransform>().anchoredPosition.y + 230;

                // Button is above the top of the view
                if (topEdge > buttonTopEdge)
                    content.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, buttonTopEdge);
                // Button is below the bottom of the view
                else if (bottomEdge < buttonBottomEdge)
                    content.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, buttonBottomEdge - 230);
            }

            // Exit
            if (GlobalControls.input.Cancel == ButtonState.PRESSED)
                ModBackground.GetComponent<Button>().onClick.Invoke();
            // Select the mod or encounter
            else if (GlobalControls.input.Confirm == ButtonState.PRESSED)
                content.transform.GetChild(selectedItem).gameObject.GetComponent<Button>().onClick.Invoke();
        }
    }

    // Shows the "mod page" screen.
    private void modFolderSelection() {
        eventSystem.SetSelectedGameObject(null);
        UnitaleUtil.printDebuggerBeforeInit = "";
        ShowMod(CurrentSelectedMod);

        //hide the 4 buttons if needed
        if (!GlobalControls.modDev)
            devMod.SetActive(false);

        //show the mod list button
        btnList.SetActive(true);

        // If the encounter box is visible, remove all encounter buttons before hiding
        if (encounterBox.activeSelf) {
            foreach (Transform b in content.transform) {
                if (b.gameObject.name != "Back")
                    Destroy(b.gameObject);
                else
                    b.GetComponent<MenuButton>().Reset();
            }
        }
        //hide the encounter selection box
        encounterBox.SetActive(false);
    }

    // Shows the list of available encounters in a mod.
    private void encounterSelection() {
        //hide the mod list button
        btnList.SetActive(false);

        //automatically choose "back"
        selectedItem = 0;

        // Make clicking the background exit the encounter selection screen
        ModBackground.GetComponent<Button>().onClick.RemoveAllListeners();
        ModBackground.GetComponent<Button>().onClick.AddListener(() => {
            eventSystem.SetSelectedGameObject(null);
            if (animationDone)
                modFolderSelection();
        });
        //show the encounter selection box
        encounterBox.SetActive(true);
        //reset the encounter box's position
        content.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 0);

        //give the back button its function
        GameObject back = content.transform.Find("Back").gameObject;
        back.GetComponent<Button>().onClick.RemoveAllListeners();
        back.GetComponent<Button>().onClick.AddListener(modFolderSelection);

        DirectoryInfo di = new DirectoryInfo(Path.Combine(FileLoader.DataRoot, "Mods/" + StaticInits.MODFOLDER + "/Lua/Encounters"));
        if (!di.Exists || di.GetFiles().Length <= 0) return;
        string[] encounters = di.GetFiles("*.lua").Select(f => Path.GetFileNameWithoutExtension(f.Name)).Where(f => !f.StartsWith("@")).ToArray();

        int count = 0;
        foreach (string encounter in encounters) {
            count += 1;

            //create a button for each encounter file
            GameObject button = Instantiate(back);

            //set parent and name
            button.transform.SetParent(content.transform);
            button.name = "EncounterButton";

            //set position
            button.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 100 - count * 30);

            //set color
            button.GetComponent<Image>().color                        = new Color(0.75f, 0.75f, 0.75f, 0.5f);
            button.GetComponent<MenuButton>().NormalColor             = new Color(0.75f, 0.75f, 0.75f, 0.5f);
            button.GetComponent<MenuButton>().HoverColor              = new Color(0.75f, 0.75f, 0.75f, 1f);
            button.transform.Find("Fill").GetComponent<Image>().color = new Color(0.5f,  0.5f,  0.5f,  0.5f);

            // set text
            button.transform.Find("Text").GetComponent<Text>().text = Path.GetFileNameWithoutExtension(encounter);
            if (GlobalControls.crate)
                button.transform.Find("Text").GetComponent<Text>().text = Temmify.Convert(Path.GetFileNameWithoutExtension(encounter), true);

            //finally, set function!
            string filename = Path.GetFileNameWithoutExtension(encounter);

            int tempCount = count;

            button.GetComponent<Button>().onClick.RemoveAllListeners();
            button.GetComponent<Button>().onClick.AddListener(() => {
                eventSystem.SetSelectedGameObject(null);
                selectedItem          = tempCount;
                StaticInits.ENCOUNTER = filename;
                StartCoroutine(LaunchMod());
            });
        }
    }

    // Opens the scrolling interface and lets the user browse their mods.
    private void modFolderMiniMenu() {
        // Hide the mod list button
        btnList.SetActive(false);

        // Automatically select the current mod when the mod list appears
        selectedItem = CurrentSelectedMod + 1;

        // Give the back button its function
        GameObject back = content.transform.Find("Back").gameObject;
        back.GetComponent<Button>().onClick.RemoveAllListeners();
        back.GetComponent<Button>().onClick.AddListener(() => {
            eventSystem.SetSelectedGameObject(null);
            // Reset the encounter box's position
            modListScroll = 0.0f;
            modFolderSelection();
        });

        // Make clicking the background exit this menu
        ModBackground.GetComponent<Button>().onClick.RemoveAllListeners();
        ModBackground.GetComponent<Button>().onClick.AddListener(() => {
            eventSystem.SetSelectedGameObject(null);
            if (!animationDone) return;
            // Store the encounter box's position so it can be remembered upon exiting a mod
            modListScroll = content.GetComponent<RectTransform>().anchoredPosition.y;
            modFolderSelection();
        });
        // Show the encounter selection box
        encounterBox.SetActive(true);
        // Move the encounter box to the stored position, for easier mod browsing
        content.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, modListScroll);

        int count = -1;
        foreach (DirectoryInfo mod in modDirs) {
            count += 1;

            // Create a button for each mod
            GameObject button = Instantiate(back);

            //set parent and name
            button.transform.SetParent(content.transform);
            button.name = "ModButton";

            //set position
            button.GetComponent<RectTransform>().anchoredPosition = new Vector2(0, 100 - (count + 1) * 30);

            //set color
            button.GetComponent<Image>().color = new Color(0.75f, 0.75f, 0.75f, 0.5f);
            button.GetComponent<MenuButton>().NormalColor = new Color(0.75f, 0.75f, 0.75f, 0.5f);
            button.GetComponent<MenuButton>().HoverColor  = new Color(0.75f, 0.75f, 0.75f, 1f);
            button.transform.Find("Fill").GetComponent<Image>().color = new Color(0.5f, 0.5f, 0.5f, 0.5f);

            //set text
            button.transform.Find("Text").GetComponent<Text>().text = mod.Name;
            if (GlobalControls.crate)
                button.transform.Find("Text").GetComponent<Text>().text = Temmify.Convert(mod.Name, true);

            //finally, set function!
            int tempCount = count;

            button.GetComponent<Button>().onClick.RemoveAllListeners();
            button.GetComponent<Button>().onClick.AddListener(() => {
                eventSystem.SetSelectedGameObject(null);
                // Store the encounter box's position so it can be remembered upon exiting a mod
                modListScroll = content.GetComponent<RectTransform>().anchoredPosition.y;

                CurrentSelectedMod = tempCount;
                modFolderSelection();
                ShowMod(CurrentSelectedMod);
            });
        }
    }
}
