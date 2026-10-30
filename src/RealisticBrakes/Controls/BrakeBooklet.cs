using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using DV.Booklets.Rendered;
using DV.CabControls;
using DV.Items;
using DV.ThingTypes;
using DV.Utils;
using HarmonyLib;
using Newtonsoft.Json.Linq;
using UnityEngine;
using ItemSpec = DV.CabControls.Spec.Item;
using TouchscreenSpec = DV.CabControls.Spec.Touchscreen;

namespace RealisticBrakes.Controls
{
 internal static class BrakeBooklet
    {
        private const string BasePrefab = "MapSchematic";

        /// <summary>The booklet's name in the inventory.</summary>
        public const string Title = "The Air-Brake Pocket-Book";

        // The schematic map's page, m: its paper mesh's 0.297 x 0.42 at the paper's 0.78 x 0.715. The page image's
        // left edge is at +x and its top at -z (read from the game's assets, 2026-10-29).
        private const float PageWidth = 0.297f * 0.78f;
        private const float PageHeight = 0.42f * 0.715f;

        private static Texture2D[] pages;
        private static JObject manifest;

        /// <summary>Whether a game is loaded to spawn the booklet in.</summary>
        public static bool CanSpawn => PlayerManager.PlayerTransform != null;

        /// <summary>Puts the booklet in front of the player.</summary>
        public static void Spawn()
        {
            if (!CanSpawn || !Load())
                return;
            var prefab = Resources.Load<GameObject>(BasePrefab);
            if (prefab == null)
            {
                Main.Mod?.Logger.Warning($"The brake booklet needs the game's {BasePrefab} booklet, which it doesn't have");
                return;
            }
            var player = PlayerManager.PlayerTransform;
            var forward = Vector3.ProjectOnPlane(player.forward, Vector3.up).normalized;
            if (forward.sqrMagnitude < 0.5f)
                forward = Vector3.forward;
            // Made inside an inactive holder, so nothing on it wakes before it is refitted. Its page's top is its -z:
            // turned so, it lands reading the right way up to the player.
            var holder = new GameObject("[Realistic Brakes booklet holder]");
            holder.SetActive(false);
            var booklet = UnityEngine.Object.Instantiate(prefab, player.position + forward * 0.6f + Vector3.up * 1.2f, Quaternion.LookRotation(-forward), holder.transform);
            try
            {
                Refit(booklet);
            }
            catch (Exception e)
            {
                Main.Mod?.Logger.Warning($"The brake booklet couldn't be made: {e.GetBaseException().GetType().Name}: {e.GetBaseException().Message}");
                UnityEngine.Object.Destroy(holder);
                return;
            }
            booklet.name = "RealisticBrakesBooklet";
            booklet.transform.SetParent(null, true);
            UnityEngine.Object.Destroy(holder);
        }

        // The pages and their layout, read once; the textures stay for the session.
        private static bool Load()
        {
            if (pages != null)
                return true;
            try
            {
                var folder = Path.Combine(Main.Mod.Path, "Booklet");
                manifest = JObject.Parse(File.ReadAllText(Path.Combine(folder, "booklet.json")));
                var files = manifest["pages"].Select(t => (string)t).ToArray();
                var loaded = new Texture2D[files.Length];
                for (var i = 0; i < files.Length; i++)
                {
                    var texture = new Texture2D(2, 2, TextureFormat.RGB24, true)
                    {
                        name = "RealisticBrakes booklet " + files[i],
                        wrapMode = TextureWrapMode.Clamp,
                        filterMode = FilterMode.Trilinear,
                        anisoLevel = 8,
                        hideFlags = HideFlags.DontUnloadUnusedAsset,
                    };
                    if (!texture.LoadImage(File.ReadAllBytes(Path.Combine(folder, files[i]))))
                        throw new InvalidDataException($"{files[i]} isn't an image");
                    // DXT1 with its mipmaps: about a megabyte a page instead of eight.
                    texture.Compress(true);
                    texture.Apply(false, true);
                    loaded[i] = texture;
                }
                pages = loaded;
                return true;
            }
            catch (Exception e)
            {
                Main.Mod?.Logger.Warning($"The brake booklet's pages couldn't be read: {e.GetBaseException().Message}");
                manifest = null;
                return false;
            }
        }

        private static void Refit(GameObject booklet)
        {
            // The map's own pages and junctions go.
            UnityEngine.Object.DestroyImmediate(booklet.GetComponent<JunctionMap>());
            UnityEngine.Object.DestroyImmediate(booklet.GetComponent<RuntimeRenderedStaticTextureBooklet>());
            var book = booklet.GetComponent<PageBook>();
            book.pageTextures = pages;
            book.currentPage = 0;
            book.generateOnStart = true;

            var spec = booklet.GetComponent<InventoryItemSpec>();
            spec.IsEssential = false;
            spec.ImmuneToDumpster = false;
            spec.BelongsToPlayer = true;
            spec.NameOverride = Title;
            // Never saved: saved, it would load as the game's map.
            booklet.GetComponent<ItemSpec>().excludeFromStorageSerialization =
                StorageType.Inventory | StorageType.LostAndFound | StorageType.World | StorageType.InstalledGadgets | StorageType.ItemContainers;

            booklet.GetComponent<CustomNonVrGrabAnchor>().customLocalRotation = new Vector3(90f, 180f, 0f);
            AccessTools.Field(typeof(ItemPlacerOffset), "customRotationOffset").SetValue(booklet.GetComponent<ItemPlacerOffset>(), new Vector3(0f, 180f, 0f));
            SetPose(booklet.transform.Find("[left anchor]"), new Vector3(0.115193f, -0.098597f, 0.103992f), new Quaternion(0.046410f, -0.995345f, -0.069298f, -0.048300f));
            SetPose(booklet.transform.Find("[right anchor]"), new Vector3(-0.115192f, -0.098595f, 0.103993f), new Quaternion(0.046410f, 0.995345f, 0.069298f, -0.048300f));
            // Its page buttons as a portrait booklet's, the game's own helper's in the bottom corners: the map's are both
            // down the right edge, on the tabs. The map's set them in from its page's corners, as BookletLinks does.
            var mapButton = book.PageFlippingHelperOverridePrefab != null ? book.PageFlippingHelperOverridePrefab.transform.Find("Next") : null;
            book.PageFlippingHelperOverridePrefab = null;

            // The thumb tabs: the cover belongs to none, and hides them.
            var tabs = (JObject)manifest["tabs"];
            var items = (JArray)tabs["items"];
            var chapters = new List<ChapterSelector.PageBookChapter> { new ChapterSelector.PageBookChapter(0, 0, false) };
            chapters.AddRange(items.Select(i => new ChapterSelector.PageBookChapter((int)i["first"], (int)i["last"], true)));
            AccessTools.Field(typeof(ChapterSelector), "allChapters").SetValue(booklet.GetComponent<ChapterSelector>(), chapters);
            var tabScreen = booklet.transform.Find("TouchscreenPagesAnchor/Touchscreen");
            Place(tabScreen, (float)tabs["x0"], (float)tabs["x1"], (float)tabs["y0"], (float)tabs["y1"], true, new Vector2Int(items.Count, 1));

            // The contents page's rows.
            var contents = (JObject)manifest["contents"];
            var targets = ((JArray)contents["targets"]).Select(t => (int)t).ToArray();
            var y0 = (float)contents["y0"];
            var rows = targets.Length * (float)contents["rowHeight"];
            var rowScreen = booklet.transform.Find("TouchScreenMapAnchor/Touchscreen");
            Place(rowScreen, (float)contents["x0"], (float)contents["x1"], y0, y0 + rows, false, new Vector2Int(1, targets.Length));
            var buttonInset = mapButton != null ? new Vector2(Mathf.Abs(mapButton.localPosition.x), Mathf.Abs(mapButton.localPosition.z)) : (Vector2?)null;
            booklet.AddComponent<BookletLinks>().Set(book, rowScreen.gameObject, (int)contents["page"], targets, buttonInset);
        }

        private static void SetPose(Transform anchor, Vector3 position, Quaternion rotation)
        {
            if (anchor == null)
                return;
            anchor.localPosition = position;
            anchor.localRotation = Quaternion.Normalize(rotation);
        }

        /// <summary>
        /// Lays <paramref name="screen"/> over the page image's rectangle (px), its sections in a
        /// <paramref name="grid"/>: along the page's height for <paramref name="tabs"/>, the first at the top; else
        /// rows down the page.
        /// </summary>
        private static void Place(Transform screen, float x0, float x1, float y0, float y1, bool tabs, Vector2Int grid)
        {
            var size = (JArray)manifest["size"];
            float w = (float)size[0], h = (float)size[1];
            var anchor = screen.parent;
            var cx = (x0 + x1) / 2;
            var cy = (y0 + y1) / 2;
            anchor.localPosition = new Vector3(PageWidth / 2 - cx / w * PageWidth, anchor.localPosition.y, -PageHeight / 2 + cy / h * PageHeight);
            // A section's x runs along the screen's local x: for the tabs that is down the page.
            anchor.localRotation = tabs ? Quaternion.Euler(0f, -90f, 0f) : Quaternion.identity;
            screen.localPosition = Vector3.zero;
            screen.localRotation = Quaternion.identity;
            var across = (x1 - x0) / w * PageWidth;
            var down = (y1 - y0) / h * PageHeight;
            var box = screen.GetComponent<BoxCollider>();
            box.center = new Vector3(0f, box.center.y, 0f);
            box.size = tabs ? new Vector3(down, box.size.y, across) : new Vector3(across, box.size.y, down);
            var spec = screen.GetComponent<TouchscreenSpec>();
            spec.gridSize = grid;
            spec.useEntireScreen = true;
            spec.flipHorizontalCoords = false;
            spec.flipVerticalCoords = false;
        }
    }

    /// <summary>
    /// The contents page's links: a press on a row turns the booklet to that row's chapter. Its touchscreen is up
    /// only while the contents page is open; the game's <see cref="ChapterSelector"/> runs the thumb tabs.
    /// </summary>
    internal sealed class BookletLinks : MonoBehaviour
    {
        private PageBook book;
        private GameObject rowScreen;
        private TouchscreenBase rows;
        private int contentsPage;
        private int[] targets;
        private Vector2? buttonInset;

        public void Set(PageBook book, GameObject rowScreen, int contentsPage, int[] targets, Vector2? buttonInset)
        {
            this.book = book;
            this.rowScreen = rowScreen;
            this.contentsPage = contentsPage;
            this.targets = targets;
            this.buttonInset = buttonInset;
        }

        // After every Awake: the touchscreen's control is added as its spec wakes.
        private IEnumerator Start()
        {
            rows = rowScreen.GetComponent<TouchscreenBase>();
            if (rows == null)
            {
                Main.Mod?.Logger.Warning("The brake booklet's contents have no touchscreen: its lines won't turn the page");
                yield break;
            }
            rows.SectionPressed += OnPressed;
            book.PageFlipped += OnFlipped;
            // A frame, then into the world's storage, as the shop delivers an item.
            yield return null;
            FitPageButtons();
            var item = GetComponent<ItemBase>();
            if (item != null)
            {
                SingletonBehaviour<StorageController>.Instance.AddItemToWorldStorage(item);
                if (item.ItemRigidbody != null)
                {
                    item.ItemRigidbody.velocity = Vector3.zero;
                    item.ItemRigidbody.angularVelocity = Vector3.zero;
                }
            }
            Notice.Show(BrakeBooklet.Title + " is in front of you");
            while (!rows.IsInitialized)
                yield return null;
            OnFlipped(book.currentPage);
        }

        private void OnDestroy()
        {
            if (rows != null)
                rows.SectionPressed -= OnPressed;
            if (book != null)
                book.PageFlipped -= OnFlipped;
        }

        // The game's page buttons, made with the pages, sit in from the corners of a larger page: set in from this one's
        // as the map's are.
        private void FitPageButtons()
        {
            var helper = buttonInset.HasValue ? GetComponentInChildren<DV.Interaction.PageFlippingHelper>(true) : null;
            if (helper == null)
                return;
            foreach (Transform button in helper.transform)
            {
                var p = button.localPosition;
                button.localPosition = new Vector3(Mathf.Sign(p.x) * buttonInset.Value.x, p.y, Mathf.Sign(p.z) * buttonInset.Value.y);
            }
        }

        private void OnFlipped(int page)
        {
            if (rows != null && rows.IsInitialized)
                rowScreen.SetActive(page == contentsPage);
        }

        private void OnPressed(Vector2Int section)
        {
            if (section.y >= 0 && section.y < targets.Length && book.currentPage == contentsPage)
                book.FlipTo(targets[section.y]);
        }
    }
}
