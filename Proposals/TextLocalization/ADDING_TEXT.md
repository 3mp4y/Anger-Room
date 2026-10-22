This guide is for someone maintaining text in Anger-Room after the proposed localization system is integrated. It is not active yet: files in `Proposals` are examples for review, and `GameTexts.asset` still needs to be created. Existing Dialogue objects are managed separately and are outside these instructions.

You will use two places in Unity: the Project window contains the shared `GameTexts` translation asset; the Hierarchy contains the objects that display text. Choose the session language on the existing `LanguageAdapter` before pressing Play.

**One-time setup for the maintainer.** Do this during the agreed integration, not every time someone adds text.

1. Keep copies of the existing source files before adapting them. Put archive copies with duplicate C# class names outside `Assets`.
2. Copy `TextId.cs`, `TextCatalog.cs`, and `TextAdapter.cs` into a new folder such as `Assets/Localization`. Leave the proposal copies in place.
3. Wait for Unity to finish compiling. Resolve any Console errors before continuing.
4. In the Project window, choose **Create > Anger Room > Text Catalog** and name the asset `GameTexts`. The same command is available under **Assets > Create**. Review the prefilled sample translations before using them.
5. Select the active GameObject that already holds `LanguageAdapter`. Add the `TextAdapter` component. Drag the existing `LanguageAdapter` component into **Language Adapter** and drag the `GameTexts` asset into **Catalog**.
6. Integrate the desired gameplay calls, using `CardTextUsageExample.cs` as a reference. Give those scripts a serialized `TextAdapter` field and assign the shared component in the Inspector. The example does not modify the actual card minigame on its own.
7. If static room labels will use this system, copy `Examples/RoomTextUsageExample.cs` into `Assets/Localization/Examples`. No existing Dialogue object needs this component.
8. Tell contributors where the catalogue asset and shared adapter are located. The workflows below assume these steps are complete.

**To change wording that already exists, no code edit is needed.**

1. Exit Play Mode so the edits are saved as project content.
2. Select `GameTexts` in the Project window.
3. Expand **Entries** in the Inspector and find the message by its **Id**, for example `Victory`.
4. Edit **English**, **Italian**, and **Spanish** as needed. Keep any placeholders such as `{0}` intact. Do not change the Id just to change the wording.
5. Save the project. Choose one language on `LanguageAdapter`, enter Play Mode, and trigger the message.
6. Exit Play Mode and repeat for the other languages. Check wording, accents, wrapping, and readability at the actual viewing distance.

Edit the asset's Inspector values, not the sample defaults in `TextCatalog.cs`. Those defaults populate newly created assets; they do not overwrite an asset that already exists. Changing the catalogue or selected language during Play Mode does not update the adapter's cached session text: stop and start Play Mode to test the change.

**To add a completely new message, first register its meaning.** This walkthrough creates a room sign saying “Choose a game.”

1. Look through the existing IDs and translations. Reuse an ID only if its meaning and usage match. Two messages that happen to share English wording can need different IDs when their contexts differ.
2. Ask the maintainer to add a new entry to the `TextId` enum, or make the small code edit yourself. For this proposal, `100` is unused:

   ```csharp
   Points = 50,
   ScoreboardEntry = 51,

   RoomWelcome = 100
   ```

   The comma after the previous entry is required. Pick a descriptive name and a number not already used. Keep existing numbers unchanged; Unity saves enum choices using these numbers. If `100` has since been used, choose another unused number.

3. Save `TextId.cs` and wait for Unity to compile. Confirm the Console has no compilation errors.
4. Select the existing `GameTexts` asset. Expand **Entries** and add one element with the **+** button or by increasing the list size.
5. Set the new element's **Id** to `RoomWelcome`. Unity may copy the last element when expanding a list, so check that you have not left a duplicate ID.
6. Fill the language fields:

   | Field | Example translation |
   | --- | --- |
   | English | Choose a game. |
   | Italian | Scegli un gioco. |
   | Spanish | Elige un juego. |

7. Save the asset. Adding the entry supplies the text; the next workflow connects it to a visible object.

A collaborator who does not edit C# only needs the maintainer for steps 2–3. They can manage the translation fields and object placement themselves. An entirely Inspector-only ID-creation workflow would require a different ID system or additional editor tooling.

**To display that message on a new static label in the room:**

1. Create a TextMeshPro object with **GameObject > 3D Object > Text - TextMeshPro**, or use **GameObject > UI > Text - TextMeshPro** within an existing suitable Canvas. For a new UI Canvas intended to sit in the room, use **World Space** render mode and position it appropriately.
2. Name the object descriptively, for example `RoomWelcomeText`. Position it, face it toward the player, and adjust its size, font, alignment, and color.
3. Add **Anger Room > Examples > Room Text Usage Example** through the object's **Add Component** menu. This is the proposed optional example component copied during setup.
4. Drag the shared `TextAdapter` component from the manager into **Text Adapter**.
5. Drag the label's `TextMeshPro` or `TextMeshProUGUI` component into **Target**. Both are supported through the common `TMP_Text` type.
6. Select `RoomWelcome` in **Text Id**. You do not paste the three translations onto the label: they are stored once in `GameTexts`.
7. Save the scene. Set `LanguageAdapter.Current Language` before Play, and check that the label displays the chosen translation. Repeat for all three languages, stopping Play Mode between them.

You can try the wiring with an existing plain ID such as `LevelLabel` before registering a new one. This label component runs when its object first becomes active, so a label that starts inactive will receive its translation when shown. It does not find or change any other scene objects.

Use this component for plain static messages with no `{0}` placeholders. If a gameplay script already writes to this text field, put localization calls in that script instead; two components writing the same field can overwrite each other.

**To display changing values, the owning script supplies the values.** A developer can follow this example for “Attempts remaining: 2.”

1. Register another unused ID, for example `RoomAttemptsNotice = 101`.
2. Add its catalogue entry, keeping the same placeholder in every language:

   | Language | Template |
   | --- | --- |
   | English | Attempts remaining: {0} |
   | Italian | Tentativi rimasti: {0} |
   | Spanish | Intentos restantes: {0} |

3. In the script that knows the remaining attempts, import the namespace and declare the references:

   ```csharp
   using AngerRoom.Localization;
   using TMPro;
   using UnityEngine;

   // Inside the existing MonoBehaviour class:
   [SerializeField] private TextAdapter textAdapter;
   [SerializeField] private TMP_Text attemptsText;
   ```

4. Assign both references in the Inspector. When the attempts value changes, use:

   ```csharp
   attemptsText.text = textAdapter.Format(
       TextId.RoomAttemptsNotice,
       remainingAttempts);
   ```

   Here `remainingAttempts` is the value already calculated by the gameplay script. `Format` translates the sentence and inserts that value; it does not calculate attempts or change the game rules.

5. Trigger the relevant game event and check several values in each language. Do not attach `RoomTextUsageExample` to the same text field.

`Get(id)` returns the selected wording without substitutions. `Format(id, valueA, valueB)` substitutes `valueA` into `{0}` and `valueB` into `{1}`. Translators can change the order of the placeholders, but must preserve their numbers and meanings. `{0:00}` means to display that value with at least two digits. A value of 3 becomes `03`.

For a plain static message, use `Get`. For a message with placeholders, use `Format` and supply the documented values. Extra arguments can be ignored by `string.Format`, and the formatter cannot tell that you supplied a score where a level was intended; the ID comments and call-site review define that contract.

In the Inspector's multiline translation fields, press Enter for an actual line break. In a C# string literal, use `\n`. Typing the two characters `\n` in the Inspector does not create a line break. For literal braces in a formatted template, write `{{` and `}}`.

**If something does not look right, use these checks:**

| What you see | What to check |
| --- | --- |
| `RoomWelcome` is missing from the dropdown | Confirm the enum was added to the integrated file under `Assets`, compilation finished, and there are no Console errors |
| `[Missing: RoomWelcome]` | Check that the catalogue contains the ID and its selected-language field is filled; also verify the adapter references the catalogue you edited |
| `[Invalid format: ...]` | Check balanced braces, placeholder numbers, and the arguments supplied to `Format` |
| `{0}` appears on screen | The text was displayed using `Get` or the static room-label example; use `Format` from the script supplying the value |
| The Console reports a duplicate ID | Check the added list element's Id; expanding the list may have copied another entry |
| The Console asks for references | Assign the label's Target and Text Adapter, then the shared adapter's Language Adapter and Catalog |
| Old wording appears after an edit | Stop and restart Play Mode; check that you edited the saved asset rather than source defaults or another asset |
| Text switches back to another message | Check whether another script or animation also controls the same label |
| The translation is clipped or characters are missing | Check the TMP text bounds, font character support, wrapping, alignment, and scale in the room |

Before handing the change over, verify that the message has a unique ID, all three translations are filled, scene references are saved, and the text is readable in all three languages. Existing Dialogue objects continue to use their manually maintained language variants.
