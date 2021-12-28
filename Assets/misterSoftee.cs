using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using KModkit;
using System.Text.RegularExpressions;
using rnd = UnityEngine.Random;

public class misterSoftee : MonoBehaviour
{
    public new KMAudio audio;
    private KMAudio.KMAudioRef motorRef;
    public KMBombInfo bomb;
    public KMBombModule module;

    public KMSelectable[] buttons;
    public KMSelectable clearButton;
    public KMSelectable submitButton;
    public KMSelectable lever;
    private Renderer[] iceCreamRenders;
    public Renderer freezerLight;
    public Texture[] iceCreamTextures;
    public TextMesh screenText;
    public TextMesh timeScreenText;
    public Transform moduleTransform;
    public Transform leverPivot;
    public GameObject logo;
    public GameObject[] hidable;
    public Color buttonRed;
    private Color buttonDefault;
    public Color[] freezerColors;

    private int[] iceCreamsPresent = new int[9];
    private int[] ordersPlaced = new int[9];
    private int[] solution = new int[9];
    private Queue<string>[] priorityLists = new Queue<string>[14];
    private int spongebobPosition;
    private int[] snDirections = new int[6];
    private int currentPosition;
    private int directionFacing;
    private int directionProgression;
    private List<int[]> pathsTaken = new List<int[]>();
    private bool duplicatedPath;
    private int excess;
    private float freezerProgession = 1f;

    private static readonly string[] iceCreamNames = new string[] { "Choco Taco", "Strawberry Shortcake", "Snow Cone", "Firecracker", "Screw Ball", "Chipwich", "King Cone", "Ice Cream Sandwich", "Push-Up Pop", "Drumstick", "Banana Fudge Bomb", "Creamsicle", "Chocolate Eclair", "Fudge Pop", "SpongeBob Bar" };
    private static readonly string[] snColumns = new string[] { "59NAU", "13SFL", "64BRO", "02QXK", "78HEM" };
    private static readonly string[][] iceCreamTable = new string[][]
    {
        new string[] { "Strawberry Shortcake", "Push-Up Pop", "Creamsicle", "Screw Ball", "Snow Cone" },
        new string[] { "Snow Cone", "Fudge Pop", "Ice Cream Sandwich", "Choco Taco", "Chipwich" },
        new string[] { "Ice Cream Sandwich", "Chocolate Eclair", "Fudge Pop", "Chipwich", "King Cone" },
        new string[] {  "King Cone", "Creamsicle", "Chocolate Eclair", "Snow Cone", "Choco Taco" },
        new string[] { "Firecracker", "Chipwich", "Banana Fudge Bomb", "Choco Taco", "Creamsicle" },
        new string[] { "Creamsicle", "Strawberry Shortcake", "Push-Up Pop", "Firecracker", "Screw Ball" },
        new string[] { "Choco Taco", "Ice Cream Sandwich", "Chipwich", "King Cone", "Strawberry Shortcake" },
        new string[] { "Banana Fudge Bomb", "Screw Ball", "Firecracker", "Creamsicle", "Drumstick" },
        new string[] { "Chocolate Eclair", "King Cone", "Screw Ball", "Banana Fudge Bomb", "Fudge Pop" },
        new string[] { "King Cone", "Firecracker", "Drumstick", "Ice Cream Sandwich", "Push-Up Pop" },
        new string[] { "Screw Ball", "Choco Taco", "Snow Cone", "Drumstick", "Ice Cream Sandwich" },
        new string[] { "Push-Up Pop", "Banana Fudge Bomb", "Drumstick", "Strawberry Shortcake", "Snow Cone" },
        new string[] { "Chipwich", "Drumstick", "Choco Taco", "Push-Up Pop", "Banana Fudge Bomb" },
        new string[] { "Drumstick", "Snow Cone", "Strawberry Shortcake", "Chipwich", "Firecracker" }
     };
    private static readonly string[] childNames = new string[] { "Alyssa", "Bobby", "Clarissa", "David", "Ellie", "Felix", "George", "Hunter", "Isabella", "Jamie", "Kyle", "Lily", "Mikey", "Noelle" };
    private static readonly string[] directionNames = new string[] { "up", "right", "down", "left" };
    private int[] startingPositions = new int[] { 6, 7, 8, 11, 12, 13, 16, 17, 18 };
    private Dictionary<string, string> childPlacements = new Dictionary<string, string>();

    private Coroutine[] buttonFadeAnimations = new Coroutine[9];
    private Coroutine leverMovement;
    private bool freezerMode;
    private int freezerProgression;
    private bool leverHeld;
    private bool melted;
    private int lastHighlightedButton = -1;
    private bool initialSoundPlayed;

    private static int moduleIdCounter = 1;
    private int moduleId;
    private bool moduleSolved;

    private void Awake()
    {
        moduleId = moduleIdCounter++;
        iceCreamRenders = buttons.Select(x => x.transform.Find("ice cream").GetComponent<Renderer>()).ToArray();
        screenText.text = "";
        buttonDefault = buttons[0].GetComponent<Renderer>().material.color;
        for (int i = 0; i < 14; i++)
            priorityLists[i] = new Queue<string>();
        SetUpDictionary();
        GetComponent<KMSelectable>().OnFocus += delegate ()
        {
            if (initialSoundPlayed)
                return;
            initialSoundPlayed = true;
            audio.PlaySoundAtTransform("jingle-start", transform);
        };

        foreach (KMSelectable button in buttons)
        {
            var ix = Array.IndexOf(buttons, button);
            button.OnHighlight += delegate ()
            {
                if (lastHighlightedButton == ix)
                    return;
                lastHighlightedButton = ix;
                var words = new string[] { "ZERO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE", "TEN" };
                screenText.text = ordersPlaced[ix] < 11 ? words[ordersPlaced[ix]] : ordersPlaced[ix].ToString();
            };
            button.OnHighlightEnded += delegate ()
            {
                screenText.text = "";
                lastHighlightedButton = -1;
            };
            button.OnInteract += delegate () { PressButton(button); return false; };
        }
        clearButton.OnInteract += delegate () { PressClearButton(); return false; };
        submitButton.OnInteract += delegate () { PressSubmitButton(); return false; };
        lever.OnInteract += delegate ()
        {
            if (!moduleSolved && freezerMode && !melted)
            {
                motorRef = audio.PlaySoundAtTransformWithRef("hum", lever.transform);
                leverHeld = true;
                audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonPress, lever.transform);
                if (leverMovement != null)
                {
                    StopCoroutine(leverMovement);
                    leverMovement = null;
                }
                leverMovement = StartCoroutine(LeverDown());
            }
            return false;
        };
        lever.OnInteractEnded += delegate ()
        {
            if (motorRef != null)
            {
                motorRef.StopSound();
                motorRef = null;
            }
            leverHeld = false;
            if (!moduleSolved && !melted && freezerMode)
                audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.BigButtonRelease, lever.transform);
            if (leverMovement != null)
            {
                StopCoroutine(leverMovement);
                leverMovement = null;
            }
            leverMovement = StartCoroutine(LeverUp());
        };
    }

    private void Start()
    {
        var tempList = Enumerable.Range(0, 14).ToList().Shuffle().Take(8).ToList();
        tempList.Add(14);
        iceCreamsPresent = tempList.Shuffle().ToArray();
        for (int i = 0; i < 9; i++)
            iceCreamRenders[i].material.mainTexture = iceCreamTextures[iceCreamsPresent[i]];
        Debug.LogFormat("[Mister Softee #{0}] Ice creams present: {1}.", moduleId, iceCreamsPresent.Select(x => iceCreamNames[x]).Join(", "));
        spongebobPosition = Array.IndexOf(iceCreamsPresent, 14);
        Debug.LogFormat("[Mister Softee #{0}] The SpongeBob Bar is in position {1}.", moduleId, spongebobPosition + 1);

        var sn = bomb.GetSerialNumber();
        for (int i = 0; i < 5; i++)
            if (snColumns[i].Any(x => sn.Contains(x)))
                for (int j = 0; j < 14; j++)
                    priorityLists[j].Enqueue(iceCreamTable[j][i]);
        var alphabet = "ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        for (int i = 0; i < 6; i++)
            snDirections[i] = alphabet.Contains(sn[i]) ? (alphabet.IndexOf(sn[i]) + 1) % 3 : int.Parse(sn[i].ToString()) % 3;
        var directionNames = new string[] { "left", "forwards", "right" };
        Debug.LogFormat("[Mister Softee #{0}] Directions obtained from the serial number: {1}.", moduleId, snDirections.Select(x => directionNames[x]).Join(", "));

        Debug.LogFormat("[Mister Softee #{0}] (Note that in the following logging, directions refer to the perspective of a bird's-eye view, and not the perspective of the driver.)", moduleId);
        currentPosition = startingPositions[spongebobPosition];
        directionFacing = bomb.GetSerialNumberLetters().Any(x => "AEIOU".Contains(x)) ? 0 : 2;
        Drive();
        duplicatedPath = false;
        while (!duplicatedPath)
            Drive();

        Debug.LogFormat("[Mister Softee #{0}] Final ice cream counts:", moduleId);
        for (int i = 0; i < 9; i++)
        {
            if (solution[i] == 0)
                continue;
            Debug.LogFormat("[Mister Softee #{0}] {1} {2}{3}.", moduleId, solution[i], iceCreamNames[iceCreamsPresent[i]], solution[i] != 1 ? "s" : "");
        }
    }

    private void Drive()
    {
        var oldPosition = currentPosition;
        var oldDirection = directionFacing;
        var offsets = new int[] { -5, 1, 5, -1 };
        currentPosition = currentPosition + offsets[directionFacing];
        var direction = snDirections[directionProgression];
        if (direction == 0)
            directionFacing = (directionFacing + 3) % 4;
        else if (direction == 2)
            directionFacing = (directionFacing + 5) % 4;
        keepTurning:
        if (directionFacing == 0 && currentPosition / 5 == 0)
        {
            directionFacing = 1;
            goto keepTurning;
        }
        if (directionFacing == 1 && currentPosition % 5 == 4)
        {
            directionFacing = 2;
            goto keepTurning;
        }
        if (directionFacing == 2 && currentPosition / 5 == 4)
        {
            directionFacing = 3;
            goto keepTurning;
        }
        if (directionFacing == 3 && currentPosition % 5 == 0)
        {
            directionFacing = 0;
            goto keepTurning;
        }
        if (Math.Abs(oldDirection - directionFacing) == 2)
        {
            directionFacing = (directionFacing + 5) % 4;
            goto keepTurning;
        }
        Debug.LogFormat("[Mister Softee #{0}] Drove {1}, now facing {2}.", moduleId, directionNames[oldDirection], directionNames[directionFacing]);

        var string1 = oldPosition + "-" + currentPosition;
        var string2 = currentPosition + "-" + oldPosition;
        if (childPlacements.ContainsKey(string1) || childPlacements.ContainsKey(string2))
        {
            var str = "";
            try
            {
                str = childPlacements[string1];
            }
            catch (KeyNotFoundException)
            {
                str = childPlacements[string2];
            }
            var ordered = new List<int>();
            foreach (char c in str)
            {
                var child = "ABCDEFGHIJKLMN".IndexOf(c);
                if (priorityLists[child].Count() == 0)
                    ordered.Add(14);
                else
                {
                    var match = false;
                    while (!match && priorityLists[child].Count() != 0)
                    {
                        var treat = priorityLists[child].Dequeue();
                        if (iceCreamsPresent.Select(x => iceCreamNames[x]).Contains(treat))
                        {
                            ordered.Add(Array.IndexOf(iceCreamNames, treat));
                            match = true;
                        }
                    }
                    if (!match)
                        ordered.Add(14);
                }
                Debug.LogFormat("[Mister Softee #{0}] {1} ordered a {2}.", moduleId, childNames[child], iceCreamNames[ordered.Last()]);
            }
            foreach (int ix in ordered)
                solution[Array.IndexOf(iceCreamsPresent, ix)]++;
        }

        directionProgression = (directionProgression + 1) % 6;
        if (pathsTaken.Any(x => x.Contains(oldPosition) && x.Contains(currentPosition)))
        {
            duplicatedPath = true;
            Debug.LogFormat("[Mister Softee #{0}] Duplicated road taken. Hit the brakes!", moduleId);
        }
        pathsTaken.Add(new int[] { oldPosition, currentPosition });
    }

    private void PressButton(KMSelectable button)
    {
        if (freezerMode)
            return;
        button.AddInteractionPunch(.25f);
        audio.PlaySoundAtTransform("press", button.transform);
        if (moduleSolved)
            return;
        var ix = Array.IndexOf(buttons, button);
        ordersPlaced[ix]++;
        var words = new string[] { "ZERO", "ONE", "TWO", "THREE", "FOUR", "FIVE", "SIX", "SEVEN", "EIGHT", "NINE", "TEN" };
        screenText.text = ordersPlaced[ix] < 11 ? words[ordersPlaced[ix]] : ordersPlaced[ix].ToString();
        button.GetComponent<Renderer>().material.color = buttonRed;
        if (buttonFadeAnimations[ix] != null)
        {
            StopCoroutine(buttonFadeAnimations[ix]);
            buttonFadeAnimations[ix] = null;
        }
        buttonFadeAnimations[ix] = StartCoroutine(FadeButton(button.GetComponent<Renderer>()));
    }

    private void PressClearButton()
    {
        if (freezerMode)
            return;
        clearButton.AddInteractionPunch(.25f);
        audio.PlaySoundAtTransform("press", clearButton.transform);
        if (moduleSolved)
            return;
        ordersPlaced = Enumerable.Repeat(0, 9).ToArray();
    }

    private void PressSubmitButton()
    {
        if (freezerMode)
            return;
        submitButton.AddInteractionPunch(.25f);
        if (moduleSolved)
            return;
        Debug.LogFormat("[Mister Softee #{0}] These amounts of ice cream were bought (reading order): {1}.", moduleId, ordersPlaced.Join(", "));
        if (Enumerable.Range(0, 9).All(x => ordersPlaced[x] == solution[x]))
        {
            Debug.LogFormat("[Mister Softee #{0}] A perfect amount of ice cream was ordered. Module solved!", moduleId);
            moduleSolved = true;
            Solve();
        }
        else if (Enumerable.Range(0, 9).Any(x => ordersPlaced[x] < solution[x]))
        {
            Debug.LogFormat("[Mister Softee #{0}] You didn't order enough ice cream. Strike!", moduleId);
            module.HandleStrike();
        }
        else
        {
            Debug.LogFormat("[Mister Softee #{0}] Too much ice cream was ordered. Freezer mode activated!", moduleId);
            freezerMode = true;
            audio.PlayGameSoundAtTransform(KMSoundOverride.SoundEffect.ButtonPress, submitButton.transform);
            for (int i = 0; i < 9; i++)
                excess += ordersPlaced[i] - solution[i];
            Debug.LogFormat("[Mister Softee #{0}] Number of extra ice creams: {1} ({2} minutes).", moduleId, excess, 2 * excess);
            timeScreenText.text = (120 * excess).ToString();
            StartCoroutine(MoveModule());
        }
    }

    private IEnumerator MoveModule()
    {
        freezerLight.material.color = freezerColors[1];
        var elapsed = 0f;
        var duration = .25f;
        while (elapsed < duration)
        {
            moduleTransform.localRotation = Quaternion.Slerp(Quaternion.Euler(0f, 0f, 0f), Quaternion.Euler(0f, 0f, 180f), elapsed / duration);
            yield return null;
            elapsed += Time.deltaTime;
        }
        moduleTransform.localEulerAngles = new Vector3(0f, 0f, 180f);
        for (int i = 0; i < 9; i++)
            buttons[i].gameObject.SetActive(false);
        clearButton.gameObject.SetActive(false);
        submitButton.gameObject.SetActive(false);
        yield return new WaitForSeconds(.1f);
        StartCoroutine(FreezerCountdown());
        StartCoroutine(ProgressFreezer());
    }

    private IEnumerator FreezerCountdown()
    {
        var timeLimit = 120 * excess;
        while (timeScreenText.text != "0")
        {
            timeScreenText.text = timeLimit.ToString();
            yield return new WaitForSeconds(1f);
            timeLimit--;
        }
        Debug.LogFormat("[Mister Softee #{0}] Module solved{1}", moduleId, melted ? "..." : "!");
        if (motorRef != null)
        {
            motorRef.StopSound();
            motorRef = null;
        }
        moduleSolved = true;
        freezerMode = false;
        for (int i = 0; i < 9; i++)
            buttons[i].gameObject.SetActive(true);
        clearButton.gameObject.SetActive(true);
        submitButton.gameObject.SetActive(true);
        var elapsed = 0f;
        var duration = .25f;
        while (elapsed < duration)
        {
            moduleTransform.localRotation = Quaternion.Slerp(Quaternion.Euler(0f, 0f, 180f), Quaternion.Euler(0f, 0f, 0f), elapsed / duration);
            yield return null;
            elapsed += Time.deltaTime;
        }
        moduleTransform.localEulerAngles = new Vector3(0f, 0f, 0f);
        yield return new WaitForSeconds(.1f);
        Solve();
    }

    private IEnumerator ProgressFreezer()
    {
        while (freezerMode && !melted)
        {
            if (freezerProgession > 0f)
            {
                if (leverHeld)
                    freezerProgession += (Time.deltaTime / 30f) * 3f;
                else
                    freezerProgession -= Time.deltaTime / 30f;
            }
            if (freezerProgession < 0f)
            {
                melted = true;
                module.HandleStrike();
                Debug.LogFormat("[Mister Softee #{0}] The ice cream melted. Strike!", moduleId);
            }
            freezerLight.material.color = Color.Lerp(freezerColors[0], freezerColors[1], freezerProgession);
            yield return null;
        }
    }

    private IEnumerator FadeButton(Renderer button)
    {
        var elapsed = 0f;
        var duration = .25f;
        while (elapsed < duration)
        {
            button.material.color = Color.Lerp(buttonRed, buttonDefault, elapsed / duration);
            yield return null;
            elapsed += Time.deltaTime;
        }
        button.material.color = buttonDefault;
    }

    private IEnumerator LeverDown()
    {
        var startAngle = leverPivot.transform.localRotation;
        var elapsed = 0f;
        var duration = .25f;
        while (elapsed < duration)
        {
            leverPivot.transform.localRotation = Quaternion.Slerp(startAngle, Quaternion.Euler(0f, 45f, 0f), elapsed / duration);
            yield return null;
            elapsed += Time.deltaTime;
        }
        leverPivot.transform.localEulerAngles = new Vector3(0f, 45f, 0f);
    }

    private IEnumerator LeverUp()
    {
        var startAngle = leverPivot.transform.localRotation;
        var elapsed = 0f;
        var duration = .75f;
        while (elapsed < duration)
        {
            leverPivot.transform.localRotation = Quaternion.Slerp(startAngle, Quaternion.Euler(0f, -45f, 0f), elapsed / duration);
            yield return null;
            elapsed += Time.deltaTime;
        }
        leverPivot.transform.localEulerAngles = new Vector3(0f, -45f, 0f);
    }

    private void Solve()
    {
        module.HandlePass();
        logo.SetActive(true);
        foreach (GameObject thing in hidable)
            thing.SetActive(false);
        audio.PlaySoundAtTransform(melted ? "jingle-solve-bad" : "jingle-solve", transform);
    }

    private void SetUpDictionary()
    {
        childPlacements.Add("1-6", "IC");
        childPlacements.Add("5-6", "I");
        childPlacements.Add("6-7", "C");
        childPlacements.Add("7-8", "M");
        childPlacements.Add("3-8", "E");
        childPlacements.Add("3-4", "E");
        childPlacements.Add("9-4", "E");
        childPlacements.Add("8-9", "EB");
        childPlacements.Add("14-9", "B");
        childPlacements.Add("14-13", "B");
        childPlacements.Add("8-13", "M");
        childPlacements.Add("12-13", "M");
        childPlacements.Add("12-11", "AK");
        childPlacements.Add("6-11", "AH");
        childPlacements.Add("10-11", "N");
        childPlacements.Add("16-11", "NK");
        childPlacements.Add("16-15", "N");
        childPlacements.Add("16-17", "K");
        childPlacements.Add("12-17", "K");
        childPlacements.Add("18-17", "GJ");
        childPlacements.Add("13-18", "G");
        childPlacements.Add("14-19", "F");
        childPlacements.Add("15-20", "D");
        childPlacements.Add("21-20", "D");
        childPlacements.Add("17-22", "J");
        childPlacements.Add("18-19", "L");
        childPlacements.Add("18-23", "JL");
        childPlacements.Add("24-23", "L");
    }

    // Twitch Plays
#pragma warning disable 414
    private readonly string TwitchHelpMessage = "!{0} press TL TM ML MM [Presses the buttons in those positions, any amount can be used] | !{0} clear [Presses the clear button] | !{0} submit [Presses the submit button] | !{0} hold <#> [In freezer mode, holds down the lever for # seconds]";
#pragma warning restore 414

    private IEnumerator ProcessTwitchCommand(string input)
    {
        input = input.Trim().ToLowerInvariant();
        var inputArray = input.Split(' ').ToArray();
        var directions = new string[] { "tl", "tm", "tr", "ml", "mm", "mr", "bl", "bm", "br" };
        yield return "strike";
        yield return "solve";
        if (input == "clear")
        {
            yield return null;
            clearButton.OnInteract();
            screenText.text = "";
        }
        else if (input == "submit")
        {
            yield return null;
            submitButton.OnInteract();
        }
        else if (inputArray.Length == 2 && inputArray[0] == "hold")
        {
            if (!freezerMode)
            {
                yield return "sendtochaterror You're not in freezer mode right now.";
                yield break;
            }
            if (melted)
            {
                yield return "sendtochaterror The ice cream already melted, don't try and get yourself out of this one.";
                yield break;
            }
            var number = 0;
            if (int.TryParse(inputArray[1], out number))
            {
                yield return null;
                lever.OnInteract();
                for (int i = 0; i < number; i++)
                    yield return new WaitForSeconds(1f);
                yield return new WaitForSeconds(.1f);
                lever.OnInteractEnded();
            }
            else
            {
                yield return "sendtochaterror That's not a valid number.";
                yield break;
            }
        }
        else if (inputArray[0] == "press" && inputArray.All(x => x == "press" || directions.Contains(x)))
        {
            yield return null;
            for (int i = 1; i < inputArray.Length; i++)
            {
                yield return "trycancel";
                yield return new WaitForSeconds(.1f);
                buttons[Array.IndexOf(directions, inputArray[i])].OnInteract();
            }
        }
        else
            yield break;
    }

    private IEnumerator TwitchHandleForcedSolve()
    {
        if (freezerMode)
        {
            if (leverHeld)
            {
                lever.OnInteractEnded();
                yield return null;
                lever.OnInteract();
            }
            else
                lever.OnInteract();
            while (!moduleSolved)
            {
                yield return true;
                yield return null;
            }
        }
        else
        {
            for (int i = 0; i < 9; i++)
            {
                if (ordersPlaced[i] > solution[i])
                {
                    clearButton.OnInteract();
                    break;
                }
            }
            yield return new WaitForSeconds(.1f);
            for (int i = 0; i < 9; i++)
            {
                while (ordersPlaced[i] != solution[i])
                {
                    buttons[i].OnInteract();
                    yield return new WaitForSeconds(.1f);
                }
            }
            submitButton.OnInteract();
        }
    }
}
