using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using YARG.Core.Chart;
using YARG.Gameplay.Player;
using YARG.Helpers.Extensions;
using YARG.Themes;

namespace YARG.Gameplay.Visuals
{
    public sealed class FiveFretNoteElement : NoteElement<GuitarNote, FiveFretPlayer>
    {
        private enum NoteType
        {
            Strum    = 0,
            HOPO     = 1,
            Tap      = 2,
            Open     = 3,
            OpenHOPO = 4,

            Count
        }

        [Space]
        [SerializeField]
        private SustainLine _normalSustainLine;
        [SerializeField]
        private SustainLine _openSustainLine;
        [SerializeField]
        private GameObject sustainEndPrefab;
        private GameObject sustainEndInstance;

        private SustainLine _sustainLine;

        [SerializeField]
        private float maxSustainLength = 0.1f;

        // Make sure the remove it later if it has a sustain
        protected override float RemovePointOffset => (float) NoteRef.TimeLength * Player.NoteSpeed;

        public override void SetThemeModels(
            Dictionary<ThemeNoteType, GameObject> models,
            Dictionary<ThemeNoteType, GameObject> starPowerModels)
        {
            CreateNoteGroupArrays((int) NoteType.Count);

            AssignNoteGroup(models, starPowerModels, (int) NoteType.Strum,    ThemeNoteType.Normal);
            AssignNoteGroup(models, starPowerModels, (int) NoteType.HOPO,     ThemeNoteType.HOPO);
            AssignNoteGroup(models, starPowerModels, (int) NoteType.Tap,      ThemeNoteType.Tap);
            AssignNoteGroup(models, starPowerModels, (int) NoteType.Open,     ThemeNoteType.Open);
            AssignNoteGroup(models, starPowerModels, (int) NoteType.OpenHOPO, ThemeNoteType.OpenHOPO);
        }

        protected override void InitializeElement()
        {
            base.InitializeElement();

            var noteGroups = NoteRef.IsStarPower ? StarPowerNoteGroups : NoteGroups;

            if (NoteRef.Fret != 0)
            {
                // Deal with non-open notes

                // Set the position
                transform.localPosition = new Vector3(GetElementX(NoteRef.Fret, 5), 0f, 0f) * LeftyFlipMultiplier;

                // Get which note model to use
                NoteGroup = NoteRef.Type switch
                {
                    GuitarNoteType.Strum => noteGroups[(int) NoteType.Strum],
                    GuitarNoteType.Hopo  => noteGroups[(int) NoteType.HOPO],
                    GuitarNoteType.Tap   => noteGroups[(int) NoteType.Strum],
                    _ => throw new ArgumentOutOfRangeException(nameof(NoteRef.Type))
                };

                _sustainLine = _normalSustainLine;
            }
            else
            {
                // Deal with open notes

                // Set the position
                transform.localPosition = Vector3.zero;

                // Get which note model to use
                NoteGroup = NoteRef.Type switch
                {
                    GuitarNoteType.Strum => noteGroups[(int) NoteType.Open],
                    GuitarNoteType.Hopo or
                    GuitarNoteType.Tap   => noteGroups[(int) NoteType.OpenHOPO],
                    _ => throw new ArgumentOutOfRangeException(nameof(NoteRef.Type))
                };

                _sustainLine = _openSustainLine;
            }

            // Show and set material properties
            NoteGroup.SetActive(true);
            NoteGroup.Initialize();

            // Set line length
            if (NoteRef.IsSustain)
            {
                _sustainLine.gameObject.SetActive(true);

                float len = (float) NoteRef.TimeLength * Player.NoteSpeed;
                _sustainLine.Initialize(len);

                if (len <= maxSustainLength + Mathf.Epsilon && sustainEndPrefab != null && sustainEndInstance == null)
                {
                    sustainEndInstance = Instantiate(sustainEndPrefab, transform);
                    sustainEndInstance.transform.localPosition = new Vector3(0f, 0f, len);

                    StartCoroutine(FadeIn(sustainEndInstance));

                    _sustainLine._lineRenderer.enabled = false;
                }
                else
                {
                    _sustainLine._lineRenderer.enabled = true;
                }
            }

            // Set note and sustain color
            UpdateColor();
        }

        public override void HitNote()
        {
            base.HitNote();

            if (!NoteRef.IsSustain)
            {
                ParentPool.Return(this);
            }
            else
            {
                HideNotes();
            }
        }

        public override void MissNote()
        {
            base.MissNote();

            if (sustainEndInstance != null)
            {
                Destroy(sustainEndInstance);
                sustainEndInstance = null;
            }

            if (NoteRef.IsSustain)
            {
                _sustainLine.gameObject.SetActive(false);
            }

            ParentPool.Return(this);
        }

        protected override void UpdateElement()
        {
            base.UpdateElement();

            UpdateSustain();
        }

        protected override void OnNoteStateChanged()
        {
            base.OnNoteStateChanged();

            UpdateColor();
        }

        public override void OnStarPowerUpdated()
        {
            base.OnStarPowerUpdated();

            UpdateColor();
        }

        private void UpdateSustain()
        {
            float adjustedSpeed = Player.NoteSpeed * GameManager.SongSpeed;

            if (_sustainLine.gameObject.activeSelf)
            {
                _sustainLine.UpdateSustainLine(adjustedSpeed);
            }

            if (sustainEndInstance != null)
            {
                float len = (float) NoteRef.TimeLength * adjustedSpeed;
                sustainEndInstance.transform.localPosition = new Vector3(0f, 0f, len);
            }
        }

        private void UpdateColor()
        {
            var colors = Player.Player.ColorProfile.FiveFretGuitar;

            // Get which note color to use
            var colorNoStarPower = colors.GetNoteColor(NoteRef.Fret);
            var color = NoteRef.IsStarPower
                ? colors.GetNoteStarPowerColor(NoteRef.Fret)
                : colorNoStarPower;

            // Set the note color
            NoteGroup.SetColorWithEmission(color.ToUnityColor(), colorNoStarPower.ToUnityColor());

            // The rest of this method is for sustain only
            if (!NoteRef.IsSustain) return;

            _sustainLine.SetState(SustainState, color.ToUnityColor());
        }

        protected override void HideElement()
        {
            HideNotes();

            _normalSustainLine.gameObject.SetActive(false);
            _openSustainLine.gameObject.SetActive(false);
        }

        public override void SustainEnd(bool finished)
        {
            if (sustainEndInstance != null)
            {
                Destroy(sustainEndInstance);
                sustainEndInstance = null;
            }

            if (NoteRef.IsSustain)
            {
                _sustainLine.gameObject.SetActive(false);
            }

            if (finished)
            {
                ParentPool.Return(this);
            }
            else
            {
                HideNotes();
            }
        }

        private IEnumerator FadeIn(GameObject obj)
        {
            Renderer renderer = obj.GetComponent<Renderer>();
            if (renderer == null) yield break;

            Material material = renderer.material;
            Color color = material.color;
    
            // Set initial alpha to 0 (fully transparent)
            color.a = 0f;
            material.color = color;

            // Fade in over time (1 second for example)
            float fadeDuration = 1.5f;
            float elapsedTime = 0f;

            while (elapsedTime < fadeDuration)
            {
                elapsedTime += Time.deltaTime;
                color.a = Mathf.Lerp(0f, 0.8f, elapsedTime / fadeDuration);
                material.color = color;

                yield return null;  // Wait for the next frame
            }

            // Ensure the alpha is set to 1 (fully visible) at the end
            color.a = 1f;
            material.color = color;
        }
    }
}