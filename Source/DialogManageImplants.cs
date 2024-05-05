using RimWorld;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Reflection;
using System.Text;
using UnityEngine;
using Verse;
using Verse.Sound;

namespace EdB.PrepareCarefully {
    public class DialogManageImplants : Window {
        public class ImplantBodyPart {
            public UniqueBodyPart UniquePart { get; set; }
            public BodyPartRecord Part {
                get {
                    return UniquePart?.Record;
                }
            }
            public bool Selected { get; set; }
            public bool Disabled { get; set; }
            public Implant Implant { get; set; }
            public Implant BlockingImplant { get; set; }
        }
        public class DialogOption {
            public ImplantOption ImplantOption { get; set; }
            public RecipeDef Recipe {
                get {
                    return ImplantOption?.RecipeDef;
                }
            }
            public bool Selected { get; set; }
            public bool PartiallySelected { get; set; }
            public bool Disabled { get; set; }
            public bool RequiresPartSelection {
                get {
                    return Parts.CountAllowNull() > 1;
                }
            }
            public string Label {
                get {
                    if (Recipe != null) {
                        return Recipe?.LabelCap;
                    }
                    if (ImplantOption?.HediffDef != null) {
                        return "EdB.PC.Dialog.Implant.InstallImplantLabel".Translate(ImplantOption.HediffDef.label);
                    }
                    else {
                        return "";
                    }
                }
            }
            public Implant BlockingImplant { get; set; }
            public List<ImplantBodyPart> Parts { get; set; } = new List<ImplantBodyPart>();
        }
        public string ConfirmButtonLabel = "EdB.PC.Dialog.Implant.Button.Confirm";
        public string CancelButtonLabel = "EdB.PC.Common.Cancel";
        public Vector2 ContentMargin { get; protected set; }
        public Vector2 WindowSize { get; protected set; }
        public Vector2 ButtonSize { get; protected set; }
        public Vector2 ContentSize { get; protected set; }
        public float HeaderHeight { get; protected set; }
        public float FooterHeight { get; protected set; }
        public float LineHeight { get; protected set; }
        public float LinePadding { get; protected set; }
        public float WindowPadding { get; protected set; }
        public Rect ContentRect { get; protected set; }
        public Rect ScrollRect { get; protected set; }
        public Rect FooterRect { get; protected set; }
        public Rect HeaderRect { get; protected set; }
        public Rect CancelButtonRect { get; protected set; }
        public Rect ConfirmButtonRect { get; protected set; }
        public Rect SingleButtonRect { get; protected set; }
        public Color DottedLineColor = new Color(60f / 255f, 64f / 255f, 67f / 255f);
        public Vector2 DottedLineSize = new Vector2(342, 2);
        protected string headerLabel;
        protected bool resizeDirtyFlag = true;
        protected bool confirmed = false;
        protected WidgetTable<DialogOption> table;
        protected List<DialogOption> options = new List<DialogOption>();
        protected List<Implant> implantList = new List<Implant>();
        protected Dictionary<BodyPartRecord, Implant> replacedParts = new Dictionary<BodyPartRecord, Implant>();
        protected CustomizedPawn customizedPawn = null;
        protected bool disabledOptionsDirtyFlag = false;
        protected List<Implant> validImplants = new List<Implant>();
        protected string cachedBlockedSelectionAlert = null;
        protected ProviderHealthOptions providerHealth = null;

        public DialogManageImplants(CustomizedPawn customizedPawn, ProviderHealthOptions providerHealth) {
            this.closeOnCancel = true;
            this.doCloseX = true;
            this.absorbInputAroundWindow = true;
            this.forcePause = true;
            this.providerHealth = providerHealth;
            InitializeWithCustomizedPawn(customizedPawn);
            Resize();
        }

        protected void InitializeWithCustomizedPawn(CustomizedPawn customizedPawn) {
            this.customizedPawn = customizedPawn;
            InitializeImplantList();
            InitializeRecipes();
            ResetDisabledState();
        }

        protected void InitializeImplantList() {
            implantList.Clear();
            replacedParts.Clear();
            foreach (var implant in customizedPawn.Customizations.Implants) {
                implantList.Add(implant);
                if (implant.ReplacesPart) {
                    replacedParts.Add(implant.BodyPartRecord, implant);
                }
            }
        }

        protected void InitializeRecipes() {
            OptionsHealth health = providerHealth.GetOptions(customizedPawn);
            this.options.Clear();
            var result = new List<DialogOption>();
            foreach (var implantOption in health.ImplantOptions) {
                var option = new DialogOption();
                option.ImplantOption = implantOption;
                option.Selected = implantList.FirstOrDefault((Implant i) => {
                    return (i.Recipe == implantOption.RecipeDef && implantOption.RecipeDef != null)
                            || (i.HediffDef == implantOption.HediffDef && implantOption.HediffDef != null);
                }) != null;
                option.Disabled = false;
                option.Parts = new List<ImplantBodyPart>();
                foreach (var part in health.FindBodyPartsForImplantRecipe(implantOption.RecipeDef)) {
                    var implantPart = new ImplantBodyPart();
                    implantPart.UniquePart = part;
                    Implant foundImplant = implantList.FirstOrDefault((Implant i) => { return i.Recipe == implantOption.RecipeDef && i.BodyPartRecord == part.Record; });
                    if (foundImplant != null) {
                        implantPart.Selected = true;
                        implantPart.Implant = foundImplant;
                    }
                    else {
                        implantPart.Selected = false;
                        implantPart.Implant = null;
                    }
                    implantPart.Disabled = false;
                    option.Parts.Add(implantPart);
                }
                if (implantOption.BodyPartRecord != null) {
                    UniqueBodyPart uniqueBodyPart = health.FindBodyPartsForRecord(implantOption.BodyPartRecord);
                    if (uniqueBodyPart != null) {
                        var implantPart = new ImplantBodyPart();
                        implantPart.UniquePart = uniqueBodyPart;
                        Implant foundImplant = implantList.FirstOrDefault((Implant i) => { return i.HediffDef == implantOption.HediffDef && i.BodyPartRecord == implantOption.BodyPartRecord; });
                        if (foundImplant != null) {
                            implantPart.Selected = true;
                            implantPart.Implant = foundImplant;
                        }
                        else {
                            implantPart.Selected = false;
                            implantPart.Implant = null;
                        }
                        implantPart.Disabled = false;
                        option.Parts.Add(implantPart);
                    }
                }
                result.Add(option);
            }
            this.options = result.OrderBy(o => o.Label).ToList();
        }
        
        protected void ResetDisabledState() {
            OptionsHealth health = providerHealth.GetOptions(customizedPawn);

            // Iterate each selected implant in order to determine if it's valid--if it's not
            // trying to replace or install on top of an already-missing part.

            // The first pass looks for duplicate implants that both try to replace the same part.
            Dictionary<BodyPartRecord, Implant> firstPassReplacedParts = new Dictionary<BodyPartRecord, Implant>();
            List<Implant> firstPassValidImplants = new List<Implant>();
            foreach (var implant in implantList) {
                UniqueBodyPart part = health.FindBodyPartsForRecord(implant.BodyPartRecord);
                if (part == null) {
                    continue;
                }
                if (firstPassReplacedParts.ContainsKey(part.Record)) {
                    continue;
                }
                firstPassValidImplants.Add(implant);
                if (implant.ReplacesPart) {
                    firstPassReplacedParts.Add(implant.BodyPartRecord, implant);
                }
            }

            // Second pass removes implants whose ancestor parts have been removed and implants
            // that don't replace parts but whose target part has been removed.
            Dictionary<BodyPartRecord, Implant> secondPassReplacedParts = new Dictionary<BodyPartRecord, Implant>();
            List<Implant> secondPassValidImplants = new List<Implant>();
            foreach (var implant in firstPassValidImplants) {
                UniqueBodyPart part = health.FindBodyPartsForRecord(implant.BodyPartRecord);
                if (part == null) {
                    continue;
                }
                bool isValid = true;
                if (!implant.ReplacesPart && firstPassReplacedParts.ContainsKey(part.Record)) {
                    isValid = false;
                }
                else {
                    foreach (var ancestor in part.Ancestors) {
                        if (firstPassReplacedParts.ContainsKey(ancestor.Record)) {
                            isValid = false;
                            break;
                        }
                    }
                }
                if (!isValid) {
                    continue;
                }
                secondPassValidImplants.Add(implant);
                if (implant.ReplacesPart) {
                    secondPassReplacedParts.Add(implant.BodyPartRecord, implant);
                }
            }
            
            // Third pass fills the final collections.
            replacedParts.Clear();
            validImplants.Clear();
            foreach (var implant in secondPassValidImplants) {
                if (implant.ReplacesPart) {
                    replacedParts.Add(implant.BodyPartRecord, implant);
                }
                validImplants.Add(implant);
            }

            //Logger.Debug("Valid implants");
            //foreach (var i in validImplants) {
            //    Logger.Debug("  " + i.recipe.LabelCap + ", " + i.PartName + (i.ReplacesPart ? ", replaces part" : ""));
            //}

            // Iterate each body part option for each recipe to determine if that body part is missing,
            // based on the whether or not it or one of its ancestors has been replaced.  Only evaluate each
            // body part once.  The result will be used to determine if recipes and part options should be
            // disabled.
            HashSet<BodyPartRecord> evaluatedParts = new HashSet<BodyPartRecord>();
            Dictionary<BodyPartRecord, Implant> blockedParts = new Dictionary<BodyPartRecord, Implant>();
            foreach (var recipe in options) {
                foreach (var part in recipe.Parts) {
                    if (evaluatedParts.Contains(part.Part)) {
                        continue;
                    }
                    if (!replacedParts.TryGetValue(part.Part, out Implant blockingImplant)) {
                        foreach (var ancestor in part.UniquePart.Ancestors) {
                            if (replacedParts.TryGetValue(ancestor.Record, out blockingImplant)) {
                                break;
                            }
                        }
                    }
                    evaluatedParts.Add(part.Part);
                    if (blockingImplant != null) {
                        blockedParts.Add(part.Part, blockingImplant);
                    }
                }
            }

            // Go through each recipe and recipe part, marking the parts as disabled if
            // they are missing and marking the recipes as disabled if all of its parts
            // are disabled.
            foreach (var option in options) {
                option.Disabled = false;
                option.BlockingImplant = null;
                int disabledCount = 0;
                int partCount = option.Parts.Count;
                foreach (var part in option.Parts) {
                    part.Disabled = false;
                    part.BlockingImplant = null;
                    Implant blockingImplant = null;
                    if (blockedParts.TryGetValue(part.Part, out blockingImplant)) {
                        if (!validImplants.Contains(part.Implant)) {
                            part.Disabled = true;
                            part.BlockingImplant = blockingImplant;
                            disabledCount++;
                            if (partCount == 1) {
                                option.BlockingImplant = blockingImplant;
                            }
                        }
                    }
                }
                if (disabledCount == option.Parts.Count) {
                    option.Disabled = true;
                }
            }

            // Evaluate each recipe's selected state.
            foreach (var recipe in options) {
                recipe.PartiallySelected = false;
                if (recipe.Selected) {
                    int selectedCount = 0;
                    foreach (var part in recipe.Parts) {
                        if (part.Selected && !part.Disabled) {
                            selectedCount++;
                            break;
                        }
                    }
                    if (selectedCount == 0) {
                        recipe.PartiallySelected = true;
                    }
                }
            }

            ResetCachedBlockedSelectionAlert();
        }

        protected void ResetCachedBlockedSelectionAlert() {
            bool showAlert = false;
            foreach (var recipe in options) {
                foreach (var part in recipe.Parts) {
                    if (part.Disabled && part.Implant != null) {
                        showAlert = true;
                        break;
                    }
                }
                if (showAlert) {
                    break;
                }
            }
            if (!showAlert) {
                cachedBlockedSelectionAlert = null;
                return;
            }
            List<Implant> blockedSelections = new List<Implant>();
            foreach (var recipe in options) {
                int partCount = recipe.Parts.Count;
                foreach (var part in recipe.Parts) {
                    if (part.Disabled && part.Implant != null) {
                        blockedSelections.Add(part.Implant);
                    }
                }
            }
            string listItems = "";
            foreach (var item in blockedSelections) {
                listItems += "\n" + "EdB.PC.Dialog.Implant.Alert.Item".Translate(item.Recipe.LabelCap, item.BodyPartRecord.def.label);
            }
            cachedBlockedSelectionAlert = "EdB.PC.Dialog.Implant.Alert".Translate(listItems);
        }

        protected void MarkDisabledOptionsAsDirty() {
            this.disabledOptionsDirtyFlag = true;
        }

        protected void EvaluateDisabledOptionsDirtyState() {
            if (disabledOptionsDirtyFlag) {
                ResetDisabledState();
                disabledOptionsDirtyFlag = false;
            }
        }

        public string HeaderLabel {
            get {
                return headerLabel;
            }
            set {
                headerLabel = value;
                MarkResizeFlagDirty();
            }
        }

        public Action<List<Implant>> CloseAction {
            get;
            set;
        }

        public Action<CustomizedPawn> SelectAction {
            get;
            set;
        }

        public override Vector2 InitialSize {
            get {
                return new Vector2(WindowSize.x, WindowSize.y);
            }
        }

        public Func<string> ConfirmValidation = () => {
            return null;
        };
        
        protected void MarkResizeFlagDirty() {
            resizeDirtyFlag = true;
        }

        public void ClickRecipeAction (DialogOption recipe) {
            if (recipe.Disabled && !recipe.Selected) {
                return;
            }
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            if (recipe.Selected) {
                recipe.Selected = false;
                foreach (var part in recipe.Parts) {
                    if (part.Selected) {
                        part.Selected = false;
                        RemoveImplant(recipe, part);
                    }
                }
            }
            else {
                recipe.Selected = true;
                if (recipe.Parts.Count == 1) {
                    recipe.Parts[0].Selected = true;
                    AddImplant(recipe, recipe.Parts[0]);
                }
            }
            MarkDisabledOptionsAsDirty();
        }

        protected void AddImplant(DialogOption option, ImplantBodyPart part) {
            Implant implant = new Implant();
            implant.Recipe = option.Recipe;
            implant.HediffDef = option.ImplantOption.HediffDef;
            implant.BodyPartRecord = part.Part;
            implantList.Add(implant);
            part.Implant = implant;
        }

        protected void RemoveImplant(DialogOption option, ImplantBodyPart part) {
            if (part.Implant != null) {
                implantList.Remove(part.Implant);
                part.Implant = null;
            }
        }

        public void ClickPartAction(DialogOption recipe, ImplantBodyPart part) {
            if (part.Disabled && !part.Selected) {
                return;
            }
            SoundDefOf.Tick_Tiny.PlayOneShotOnCamera();
            if (part.Selected) {
                part.Selected = false;
                RemoveImplant(recipe, part);
            }
            else {
                part.Selected = true;
                AddImplant(recipe, part);
            }
            MarkDisabledOptionsAsDirty();
        }

        protected void Resize() {
            float headerSize = 0;
            headerSize = HeaderHeight;
            if (HeaderLabel != null) {
                headerSize = HeaderHeight;
            }

            LineHeight = 30;
            LinePadding = 2;
            HeaderHeight = 32;
            FooterHeight = 40f;
            WindowPadding = 18;
            ContentMargin = new Vector2(10f, 18f);
            WindowSize = new Vector2(440f, 584f);
            ButtonSize = new Vector2(140f, 40f);

            ContentSize = new Vector2(WindowSize.x - WindowPadding * 2 - ContentMargin.x * 2,
                WindowSize.y - WindowPadding * 2 - ContentMargin.y * 2 - FooterHeight - headerSize);

            ContentRect = new Rect(ContentMargin.x, ContentMargin.y + headerSize, ContentSize.x, ContentSize.y);

            ScrollRect = new Rect(0, 0, ContentRect.width, ContentRect.height);

            HeaderRect = new Rect(ContentMargin.x, ContentMargin.y, ContentSize.x, HeaderHeight);

            FooterRect = new Rect(ContentMargin.x, ContentRect.y + ContentSize.y + 20,
                ContentSize.x, FooterHeight);

            SingleButtonRect = new Rect(ContentSize.x / 2 - ButtonSize.x / 2,
                (FooterHeight / 2) - (ButtonSize.y / 2),
                ButtonSize.x, ButtonSize.y);

            CancelButtonRect = new Rect(0,
                (FooterHeight / 2) - (ButtonSize.y / 2),
                ButtonSize.x, ButtonSize.y);
            ConfirmButtonRect = new Rect(ContentSize.x - ButtonSize.x,
                (FooterHeight / 2) - (ButtonSize.y / 2),
                ButtonSize.x, ButtonSize.y);

            Vector2 portraitSize = new Vector2(70, 70);
            float radioWidth = 36;
            Vector2 nameSize = new Vector2(ContentRect.width - portraitSize.x - radioWidth, portraitSize.y * 0.5f);

            table = new WidgetTable<DialogOption>();
            table.Rect = new Rect(Vector2.zero, ContentRect.size);
            table.RowHeight = LineHeight;
            table.RowColor = new Color(0, 0, 0, 0);
            table.AlternateRowColor = new Color(0, 0, 0, 0);
            table.SelectedAction = (DialogOption recipe) => {
            };
            table.AddColumn(new WidgetTable<DialogOption>.Column() {
                Name = "Recipe",
                AdjustForScrollbars = true,
                DrawAction = (DialogOption option, Rect rect, WidgetTable<DialogOption>.Metadata metadata) => {
                    GUI.color = Color.white;
                    Text.Anchor = TextAnchor.LowerLeft;
                    Rect labelRect = new Rect(rect.x, rect.y, rect.width, LineHeight);
                    Rect dottedLineRect = new Rect(labelRect.x, labelRect.y + 21, DottedLineSize.x, DottedLineSize.y);
                    Rect checkboxRect = new Rect(labelRect.width - 22 - 6, labelRect.MiddleY() - 12, 22, 22);
                    Rect clickRect = new Rect(labelRect.x, labelRect.y, labelRect.width - checkboxRect.width, labelRect.height);
                    GUI.color = DottedLineColor;
                    GUI.DrawTexture(dottedLineRect, Textures.TextureDottedLine);
                    Vector2 labelSize = Text.CalcSize(option.Label);
                    GUI.color = Style.ColorWindowBackground;
                    GUI.DrawTexture(new Rect(labelRect.x, labelRect.y, labelSize.x + 2, labelRect.height), BaseContent.WhiteTex);
                    GUI.DrawTexture(checkboxRect.InsetBy(-2, -2, -40, -2), BaseContent.WhiteTex);
                    if (!option.Disabled) {
                        Style.SetGUIColorForButton(labelRect, option.Selected, Style.ColorText, Style.ColorButtonHighlight, Style.ColorButtonHighlight);
                        Widgets.Label(labelRect, option.Label);
                        if (Widgets.ButtonInvisible(clickRect)) {
                            ClickRecipeAction(option);
                        }
                        GUI.color = Color.white;
                        Texture2D checkboxTexture = Textures.TextureCheckbox;
                        if (option.PartiallySelected) {
                            checkboxTexture = Textures.TextureCheckboxPartiallySelected;
                        }
                        else if (option.Selected) {
                            checkboxTexture = Textures.TextureCheckboxSelected;
                        }
                        if (Widgets.ButtonImage(checkboxRect, checkboxTexture)) {
                            ClickRecipeAction(option);
                        }
                    }
                    else {
                        GUI.color = Style.ColorControlDisabled;
                        Widgets.Label(labelRect, option.Label);
                        GUI.DrawTexture(checkboxRect, option.Selected ? Textures.TextureCheckboxPartiallySelected : Textures.TextureCheckbox);
                        if (Widgets.ButtonInvisible(checkboxRect)) {
                            ClickRecipeAction(option);
                        }
                        if (option.BlockingImplant != null) {
                            TooltipHandler.TipRegion(labelRect, "EdB.PC.Dialog.Implant.Conflict".Translate(option.BlockingImplant.Recipe.LabelCap, option.BlockingImplant.BodyPartRecord.Label));
                        }
                    }
                    if (option.Selected && option.RequiresPartSelection) {
                        float partInset = 32;
                        float cursor = labelRect.yMax;
                        foreach (var part in option.Parts) {
                            string labelText = part.Part.LabelCap;
                            labelRect = new Rect(rect.x + partInset, cursor, rect.width - partInset * 2, LineHeight);
                            dottedLineRect = new Rect(labelRect.x, labelRect.y + 21, DottedLineSize.x, DottedLineSize.y);
                            checkboxRect = new Rect(labelRect.x + labelRect.width - 22 - 6, labelRect.MiddleY() - 12, 22, 22);
                            clickRect = new Rect(labelRect.x, labelRect.y, labelRect.width - checkboxRect.width, labelRect.height);
                            GUI.color = DottedLineColor;
                            GUI.DrawTexture(dottedLineRect, Textures.TextureDottedLine);
                            labelSize = Text.CalcSize(labelText);
                            GUI.color = Style.ColorWindowBackground;
                            GUI.DrawTexture(new Rect(labelRect.x, labelRect.y, labelSize.x + 2, labelRect.height), BaseContent.WhiteTex);
                            GUI.DrawTexture(checkboxRect.InsetBy(-2, -2, -80, -2), BaseContent.WhiteTex);
                            if (!part.Disabled) {
                                Style.SetGUIColorForButton(labelRect, part.Selected, Style.ColorText, Style.ColorButtonHighlight, Style.ColorButtonHighlight);
                                Widgets.Label(labelRect, labelText);
                                if (Widgets.ButtonInvisible(clickRect)) {
                                    ClickPartAction(option, part);
                                }
                                GUI.color = Color.white;
                                if (Widgets.ButtonImage(checkboxRect, part.Selected ? Textures.TextureCheckboxSelected : Textures.TextureCheckbox)) {
                                    ClickPartAction(option, part);
                                }
                            }
                            else {
                                GUI.color = Style.ColorControlDisabled;
                                Widgets.Label(labelRect, labelText);
                                GUI.DrawTexture(checkboxRect, part.Selected ? Textures.TextureCheckboxPartiallySelected : Textures.TextureCheckbox);
                                if (Widgets.ButtonInvisible(checkboxRect)) {
                                    ClickPartAction(option, part);
                                }
                                if (part.BlockingImplant != null) {
                                    TooltipHandler.TipRegion(labelRect, "EdB.PC.Dialog.Implant.Conflict".Translate(part.BlockingImplant.Recipe.LabelCap, part.BlockingImplant.BodyPartRecord.Label));
                                }
                            }
                            cursor += labelRect.height;
                        }
                    }
                    Text.Anchor = TextAnchor.UpperLeft;
                },
                MeasureAction = (DialogOption recipe, float width, WidgetTable<DialogOption>.Metadata metadata) => {
                    if (recipe.Selected && recipe.Parts.Count > 1) {
                        return LineHeight + (LineHeight * recipe.Parts.Count);
                    }
                    else {
                        return LineHeight;
                    }
                },
                Width = ContentSize.x
            });

            resizeDirtyFlag = false;
        }

        public override void DoWindowContents(Rect inRect) {
            if (resizeDirtyFlag) {
                Resize();
            }
            EvaluateDisabledOptionsDirtyState();
            GUI.color = Color.white;
            Text.Font = GameFont.Medium;
            if (HeaderLabel != null) {
                Rect headerRect = HeaderRect;
                if (cachedBlockedSelectionAlert != null) {
                    Rect alertRect = new Rect(headerRect.xMin, headerRect.yMin + 5, 20, 20);
                    GUI.DrawTexture(alertRect, Textures.TextureAlertSmall);
                    TooltipHandler.TipRegion(alertRect, cachedBlockedSelectionAlert);
                    headerRect = headerRect.InsetBy(26, 0, 0, 0);
                }
                Widgets.Label(headerRect, HeaderLabel);
            }

            Text.Font = GameFont.Small;
            GUI.BeginGroup(ContentRect);

            try {
                table.Draw(this.options);
            }
            finally {
                GUI.EndGroup();
                GUI.color = Color.white;
            }

            GUI.BeginGroup(FooterRect);
            try {
                Rect buttonRect = SingleButtonRect;
                if (CancelButtonLabel != null) {
                    if (Widgets.ButtonText(CancelButtonRect, CancelButtonLabel.Translate(), true, true, true)) {
                        this.Close(true);
                    }
                    buttonRect = ConfirmButtonRect;
                }
                if (Widgets.ButtonText(buttonRect, ConfirmButtonLabel.Translate(), true, true, true)) {
                    string validationMessage = ConfirmValidation();
                    if (validationMessage != null) {
                        Messages.Message(validationMessage.Translate(), MessageTypeDefOf.RejectInput);
                    }
                    else {
                        this.Confirm();
                    }
                }
            }
            finally {
                GUI.EndGroup();
            }
        }

        protected void Confirm() {
            confirmed = true;
            this.Close(true);
        }

        public override void PostClose() {
            if (ConfirmButtonLabel != null) {
                if (confirmed && CloseAction != null) {
                    CloseAction(validImplants);
                }
            }
            else {
                if (CloseAction != null) {
                    CloseAction(validImplants);
                }
            }
        }
    }
}
