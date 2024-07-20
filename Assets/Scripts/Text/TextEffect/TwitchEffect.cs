﻿using UnityEngine;

public class TwitchEffect : TextEffect {
    private int selectedChar = -1;
    private int updateCount;
    private readonly float intensity;
    private readonly int avgWigFrames = 48;
    private readonly int wigFrameVariety = 16;
    private int nextWigInFrames;
    private LuaSpriteController ctrl;

    public TwitchEffect(TextManager textMan, float intensity = 2.0f, int step = 0) : base(textMan) {
        this.intensity = intensity > 0 ? intensity : 2.0f;

        if (step > 0) {
            avgWigFrames = step;
            wigFrameVariety = step / 3;
        }
        nextWigInFrames = GetNextWigTime();
    }

    protected override void UpdateInternal() {
        if (textMan.letters.Count == 0)
            return;

        // Move back last character
        if (updateCount == 0 && selectedChar >= 0 && selectedChar < textMan.letters.Count && ctrl != null) {
            ctrl.Move(-positions[selectedChar].x, -positions[selectedChar].y);
            positions[selectedChar] = new Vector2();
        }

        updateCount++;
        if (updateCount < nextWigInFrames)
            return;
        updateCount = 0;
        nextWigInFrames = GetNextWigTime();

        float random = Random.value * 2.0f * Mathf.PI;
        selectedChar = Random.Range(0, textMan.letters.Count);
        positions[selectedChar] = new Vector2(Mathf.Sin(random) * intensity, Mathf.Cos(random) * intensity);

        ctrl = LuaSpriteController.GetOrCreate(textMan.letters[selectedChar].image.gameObject);
        ctrl.Move(positions[selectedChar].x, positions[selectedChar].y);
    }

    private int GetNextWigTime() { return avgWigFrames + Mathf.RoundToInt(wigFrameVariety * (Random.value * 2 - 1)); }
}