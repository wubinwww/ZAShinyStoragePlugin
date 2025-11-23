using PKHeX.Core;
using PKHeX.Drawing.PokeSprite;
using ZAShinyWarper;

namespace ZAShinyStoragePlugin
{
    public record CachedShiny(ulong LocationHash, PA9 Pokemon);

    public partial class ShinyStorageForm : Form
    {
        private const int ENTRY_COUNT = 10;
        private const int ENTRY_SIZE = 0x1F0;
        private const int SIZE_9PARTY = 0x158;
        private const int POKEMON_DATA_OFFSET = 8;
        private const int LOCATION_HASH_SIZE = 8;
        private static readonly byte[] StartEndMarker = [0x45, 0x26, 0x22, 0x84, 0xE4, 0x9C, 0xF2, 0xCB];

        private readonly List<PictureBox> StoredShinies;
        private readonly List<CachedShiny?> CachedPokemon = [];
        private readonly SAV9ZA? SAV;
        private readonly SCBlock? ShinyBlock;
        private int SelectedSlot = -1;
        private bool SaveModified = false;

        private float zoomFactor = 1.0f;
        private Point imageLocation = Point.Empty;
        private Point mouseDownLocation = Point.Empty;
        private bool isPanning = false;

        private float MIN_ZOOM = 0.1f;
        private const float MAX_ZOOM = 10.0f;
        private const float ZOOM_STEP = 0.1f;


        private readonly GameStrings Strings = GameInfo.Strings;

        public ShinyStorageForm(SAV9ZA sav9za)
        {
            InitializeComponent();
            FitImageToBox();
            SAV = sav9za;
            ShinyBlock = SAV.Accessor.GetBlock(SaveBlockAccessor9ZA.KStoredShinyEntity);
            StoredShinies = [StoredShiny1, StoredShiny2, StoredShiny3, StoredShiny4, StoredShiny5,
                           StoredShiny6, StoredShiny7, StoredShiny8, StoredShiny9, StoredShiny10];
            for (int i = 0; i < StoredShinies.Count; i++)
            {
                StoredShinies[i].Tag = i;
                StoredShinies[i].Cursor = Cursors.Hand;
            }
            var parent = FindForm();
            if (parent != null)
                this.CenterToForm(parent);
            FormClosed += ShinyStorageForm_FormClosed;
        }

        public void LoadPokemon()
        {
            CachedPokemon.Clear();

            for (int i = 0; i < ENTRY_COUNT; i++)
            {
                int entryOffset = i * ENTRY_SIZE;

                var hashSlice = ShinyBlock!.Data.Slice(entryOffset, LOCATION_HASH_SIZE);
                ulong locationHash = BitConverter.ToUInt64(hashSlice);

                var pokemonSlice = ShinyBlock.Data.Slice(entryOffset + POKEMON_DATA_OFFSET, SIZE_9PARTY);

                CachedShiny? cachedShiny = null;
                if (EntityDetection.IsPresent(pokemonSlice))
                {
                    var pk = new PA9(pokemonSlice.ToArray());
                    cachedShiny = new CachedShiny(locationHash, pk);
                }

                CachedPokemon.Add(cachedShiny);
            }

            PopulateStoredShinies();
            UpdateStatusLabel(CachedPokemon.Count(p => p != null));
        }

        private void PopulateStoredShinies()
        {
            for (int i = 0; i < ENTRY_COUNT; i++)
            {
                var pb = StoredShinies[i];
                var cachedShiny = CachedPokemon[i];

                if (cachedShiny != null)
                {
                    pb.Image = cachedShiny.Pokemon.Sprite();
                    pb.BackColor = Color.LightYellow;
                }
                else
                {
                    pb.Image = null;
                    pb.BackColor = Color.WhiteSmoke;
                }
            }
        }

        private void UpdateStatusLabel(int occupiedCount)
        {
            if (lbl_Status != null)
            {
                lbl_Status.Text = $"Stored Shinies: {occupiedCount} / {ENTRY_COUNT}";
                lbl_Status.ForeColor = occupiedCount > 0 ? Color.DarkGreen : Color.Gray;
            }
        }

        private void Slot_Click(object? sender, EventArgs e)
        {
            if (sender is not PictureBox pb || pb.Tag is not int slot)
                return;

            // Deselect previous slot
            if (SelectedSlot >= 0 && SelectedSlot < StoredShinies.Count)
            {
                var prevEntry = CachedPokemon[SelectedSlot];
                StoredShinies[SelectedSlot].BackColor = prevEntry != null ? Color.LightYellow : Color.WhiteSmoke;
            }

            // Select new slot
            SelectedSlot = slot;
            pb.BackColor = Color.LightBlue;

            var shiny = CachedPokemon[slot];
            if (shiny != null)
            {
                LocateAndDisplay(sender, e);
                btn_RemoveSelected.Enabled = true;
            }
            else
            {
                btn_RemoveSelected.Enabled = false;
            }
        }

        private void LocateAndDisplay(object? sender, EventArgs e)
        {
            if (sender is PictureBox pb)
            {
                int index = StoredShinies.IndexOf(pb);
                var shiny = CachedPokemon[index];
                var pkColor = (PersonalColor)shiny!.Pokemon.PersonalInfo.Color;
                var color = Color.FromName(pkColor.ToString());
                if (shiny == null)
                    return;

                if (LocationParser.MainSpawnerCoordinates != null && LocationParser.MainSpawnerCoordinates.TryGetValue($"{shiny.LocationHash:X16}", out var location))
                {
                    var img = Properties.Resources.lumiose;
                    PointRenderer.RenderPoint(location, LumioseFieldIndex.Overworld, img, color);
                    pbMap.Image = img;
                }
                else if (LocationParser.Sewers1SpawnerCoordinates != null && LocationParser.Sewers1SpawnerCoordinates.TryGetValue($"{shiny.LocationHash:X16}", out location))
                {
                    var img = Properties.Resources.the_sewers_ch5;
                    PointRenderer.RenderPoint(location, LumioseFieldIndex.SewersCh5, img, color);
                    pbMap.Image = img;

                }
                else if (LocationParser.Sewers2SpawnerCoordinates != null && LocationParser.Sewers2SpawnerCoordinates.TryGetValue($"{shiny.LocationHash:X16}", out location))
                {
                    var img = Properties.Resources.the_sewers_ch6;
                    PointRenderer.RenderPoint(location, LumioseFieldIndex.SewersCh6, img, color);
                    pbMap.Image = img;
                }
                else if (LocationParser.LysandreSpawnerCoordinates != null && LocationParser.LysandreSpawnerCoordinates.TryGetValue($"{shiny.LocationHash:X16}", out location))
                {
                    var img = Properties.Resources.lysandre_labs;
                    PointRenderer.RenderPoint(location, LumioseFieldIndex.LysandreLabs, img, color);
                    pbMap.Image = img;
                }
                else
                {
                    BeginInvoke(() =>
                    {
                        MessageBox.Show($"Unable to determine spawn location", "Unable to find!", MessageBoxButtons.OK, MessageBoxIcon.Warning);
                    });
                    return;
                }

                FitImageToBox();
            }
        }

        private void OnMouseHover(object sender, EventArgs e)
        {
            PictureBox pb = (PictureBox)sender;
            Invoke(() =>
            {
                pb.BorderStyle = BorderStyle.FixedSingle;
                ShinyInfo.Hide(pb);
                ShinyInfo.Active = false;
                ShinyInfo.Active = true;
            });

            int index = StoredShinies.IndexOf(pb);
            if (index >= 0 && index < CachedPokemon.Count(p => p != null))
            {
                var pk = CachedPokemon[index]!.Pokemon;
                var species = Strings.Species[pk.Species];
                var formName = ShowdownParsing.GetStringFromForm(pk.Form, Strings, pk.Species, pk.Context);
                if (!string.IsNullOrEmpty(formName))
                    species += $"-{formName}";
                var ability = Strings.Ability[pk.Ability];
                var genderStr = pk.Gender == 0 ? "♂ Male" : pk.Gender == 1 ? "♀ Female" : "Genderless";

                var details = $"{species}{Environment.NewLine}";
                details += $"Level: {pk.CurrentLevel}{Environment.NewLine}";
                details += $"Gender: {genderStr}{Environment.NewLine}";
                details += $"Nature: {pk.Nature}{Environment.NewLine}";
                details += $"Ability: {ability}{Environment.NewLine}";
                details += $"IVs: {pk.IV_HP}/{pk.IV_ATK}/{pk.IV_DEF}/{pk.IV_SPA}/{pk.IV_SPD}/{pk.IV_SPE}{Environment.NewLine}";

                for (int i = 0; i < 4; i++)
                {
                    var move = pk.GetMove(i);
                    if (move > 0)
                    {
                        var moveName = Strings.Move[move];
                        details += $"- {moveName}{Environment.NewLine}";
                    }
                }

                if (InvokeRequired)
                    Invoke(() => ShinyInfo.SetToolTip(pb, details));
                else
                    ShinyInfo.SetToolTip(pb, details);

            }
            else
            {
                if (InvokeRequired)
                    Invoke(() => ShinyInfo.SetToolTip(pb, "No shiny data"));
                else
                    ShinyInfo.SetToolTip(pb, "No shiny data");
            }
        }

        private void Btn_RemoveSelected_Click(object? sender, EventArgs e)
        {
            if (SelectedSlot < 0)
                return;

            if (CachedPokemon[SelectedSlot] == null)
                return;

            var cachedShiny = CachedPokemon[SelectedSlot];
            var species = Strings.Species[cachedShiny!.Pokemon.Species];
            var result = MessageBox.Show(
                $"Are you sure you want to remove this shiny Pokemon from the cache?\n\n" +
                $"{species}\n" +
                $"Level: {cachedShiny.Pokemon.CurrentLevel}",
                "Confirm Removal",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Question);

            if (result != DialogResult.Yes)
                return;

            ClearSlot(SelectedSlot);
            ApplyChanges(ShinyBlock!.Data);
            LoadPokemon();

            MessageBox.Show("Pokemon removed successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void Btn_RemoveAll_Click(object? sender, EventArgs e)
        {
            int occupiedCount = CachedPokemon.Count(entry => entry != null);

            if (occupiedCount == 0)
            {
                MessageBox.Show("There are no stored shiny Pokemon to clear.", "Info", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }

            var result = MessageBox.Show(
                $"Are you sure you want to clear ALL {occupiedCount} stored shiny Pokemon?\n\n" +
                "This action cannot be undone!",
                "Confirm Clear All",
                MessageBoxButtons.YesNo,
                MessageBoxIcon.Warning);

            if (result != DialogResult.Yes)
                return;

            for (int i = 0; i < ENTRY_COUNT; i++)
                CachedPokemon[i] = null;

            ClearAllSlots();
            ApplyChanges(ShinyBlock!.Data);
            LoadPokemon();

            MessageBox.Show("All entries cleared successfully.", "Success", MessageBoxButtons.OK, MessageBoxIcon.Information);
        }

        private void PbMap_MouseWheel(object? sender, MouseEventArgs e)
        {
            var image = pbMap.Image ?? pbMap.BackgroundImage;
            if (image == null) return;

            float oldZoom = zoomFactor;
            if (e.Delta > 0)
                zoomFactor += ZOOM_STEP;
            else
                zoomFactor -= ZOOM_STEP;

            zoomFactor = Math.Max(MIN_ZOOM, Math.Min(MAX_ZOOM, zoomFactor));

            if (oldZoom != zoomFactor)
            {
                float zoomRatio = zoomFactor / oldZoom;
                imageLocation.X = (int)(e.X - (e.X - imageLocation.X) * zoomRatio);
                imageLocation.Y = (int)(e.Y - (e.Y - imageLocation.Y) * zoomRatio);
            }

            pbMap.Invalidate();
        }

        private void PbMap_MouseDown(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isPanning = true;
                mouseDownLocation = e.Location;
                pbMap.Cursor = Cursors.SizeAll;
            }
        }

        private void PbMap_MouseMove(object? sender, MouseEventArgs e)
        {
            if (isPanning)
            {
                int deltaX = e.X - mouseDownLocation.X;
                int deltaY = e.Y - mouseDownLocation.Y;

                imageLocation.X += deltaX;
                imageLocation.Y += deltaY;

                mouseDownLocation = e.Location;
                pbMap.Invalidate();
            }
        }

        private void PbMap_MouseUp(object? sender, MouseEventArgs e)
        {
            if (e.Button == MouseButtons.Left)
            {
                isPanning = false;
                pbMap.Cursor = Cursors.Default;
            }
        }

        private void PbMap_Paint(object? sender, PaintEventArgs e)
        {
            var image = pbMap.Image;
            if (image == null) return;

            e.Graphics.Clear(pbMap.BackColor);
            e.Graphics.InterpolationMode = System.Drawing.Drawing2D.InterpolationMode.HighQualityBicubic;
            e.Graphics.SmoothingMode = System.Drawing.Drawing2D.SmoothingMode.HighQuality;
            e.Graphics.PixelOffsetMode = System.Drawing.Drawing2D.PixelOffsetMode.HighQuality;
            e.Graphics.CompositingQuality = System.Drawing.Drawing2D.CompositingQuality.HighQuality;

            int width = (int)(image.Width * zoomFactor);
            int height = (int)(image.Height * zoomFactor);
            Rectangle destRect = new(imageLocation.X, imageLocation.Y, width, height);
            e.Graphics.DrawImage(image, destRect);
        }

        private void FitImageToBox()
        {
            var image = pbMap.Image;
            if (image == null) return;

            float scaleX = (float)pbMap.Width / image.Width;
            float scaleY = (float)pbMap.Height / image.Height;

            zoomFactor = Math.Min(scaleX, scaleY);
            MIN_ZOOM = zoomFactor * 0.5f;
            int scaledWidth = (int)(image.Width * zoomFactor);
            int scaledHeight = (int)(image.Height * zoomFactor);

            imageLocation.X = (pbMap.Width - scaledWidth) / 2;
            imageLocation.Y = (pbMap.Height - scaledHeight) / 2;

            pbMap.Invalidate();
        }

        public void ResetMapZoom(object? sender, EventArgs e)
        {
            FitImageToBox();
        }

        private void ClearEntry(int index)
        {
            if (index < 0 || index >= ENTRY_COUNT)
                return;

            // Slice the entry
            var slice = ShinyBlock!.Data.Slice(index * ENTRY_SIZE, ENTRY_SIZE);

            // Format the entry
            slice.Clear();
            StartEndMarker.CopyTo(slice[..StartEndMarker.Length]);
            StartEndMarker.CopyTo(slice.Slice(ENTRY_SIZE - StartEndMarker.Length, StartEndMarker.Length));
            slice[424] = 0x01;

            // Clear cached Pokémon reference
            CachedPokemon[index] = null;
        }

        private void ClearSlot(int index)
        {
            if (index < 0 || index >= ENTRY_COUNT)
                return;

            // Shift subsequent entries up
            var data = ShinyBlock!.Data;
            for (int src = index + 1; src < ENTRY_COUNT; src++)
            {
                int srcStart = src * ENTRY_SIZE;
                int destStart = (src - 1) * ENTRY_SIZE;
                data.Slice(srcStart, ENTRY_SIZE).CopyTo(data.Slice(destStart, ENTRY_SIZE));

                CachedPokemon[src - 1] = CachedPokemon[src];
            }

            // Clear the previous slot after shifting
            ClearEntry(ENTRY_COUNT - 1);

            ApplyChanges(data);
            PopulateStoredShinies();
        }

        private void ClearAllSlots()
        {
            for (int i = 0; i < ENTRY_COUNT; i++)
                ClearEntry(i);

            ApplyChanges(ShinyBlock!.Data);
            PopulateStoredShinies();
        }

        private void ApplyChanges(Span<byte> data)
        {
            ShinyBlock!.ChangeData(data);
            SAV!.State.Edited = true;
            SaveModified = true;
        }

        private void ShinyStorageForm_FormClosed(object? sender, FormClosedEventArgs e)
        {
            // Only reload if changes were made, and form is now fully closed
            if (SaveModified)
            {
                ReloadSaveInPKHeX();
            }
        }

        private void ReloadSaveInPKHeX()
        {
            try
            {
                Form? mainForm = null;

                foreach (Form form in Application.OpenForms)
                {
                    if (form.GetType().Name == "Main")
                    {
                        mainForm = form;
                        break;
                    }
                }

                if (mainForm == null)
                    return;

                var type = mainForm.GetType();
                var openSavMethod = type.GetMethod("OpenSAV",
                    System.Reflection.BindingFlags.NonPublic |
                    System.Reflection.BindingFlags.Instance,
                    null,
                    [typeof(SaveFile), typeof(string)],
                    null);

                if (openSavMethod != null)
                {
                    var path = SAV!.Metadata.FilePath ?? string.Empty;
                    openSavMethod.Invoke(mainForm, [SAV, path]);
                }
            }
            catch (Exception ex)
            {
                // Log for debugging but don't bother the user - changes are applied, the Other tab is just not updated
                System.Diagnostics.Debug.WriteLine($"Auto-reload failed: {ex.Message}");
            }
        }
    }
}