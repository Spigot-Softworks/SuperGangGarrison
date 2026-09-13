using Microsoft.Xna.Framework;
using OpenGarrison.Client;
using OpenGarrison.ClientShared;
using OpenGarrison.Core;
using Xunit;

namespace OpenGarrison.PluginHost.Tests;

public sealed class FirstPlayHintsTests
{
    [Fact]
    public void EmbeddedBuilderTemplatesKeepTheirExactPhrasingPresentationAndOrder()
    {
        var messages = FirstPlayMessageCatalog.Messages;
        Assert.Equal(new[] { "Press 'Spacebar' to Use Your Special Ability", "Press 'Q' to Equip Your Secondary",
            "Press 'Q' Near a Cabinet to Exchange Primaries (Runner, Rifleman, Healer)" }, messages.Select(m => m.Text));
        foreach (var marker in messages)
        {
            Assert.Equal(GameplayMessageStyle.Chat, marker.Style);
            Assert.Equal(GameplayMessageAnimation.FromLeft, marker.Animation);
            Assert.Equal(GameplayMessageFont.Default, marker.Font);
            Assert.Equal(1f, marker.FontScale);
            Assert.Equal(GameplayMessageAlignment.Center, marker.Alignment);
            Assert.Equal(GameplayMessageChatTeam.Auto, marker.ChatTeam);
            Assert.Equal(4f, marker.DurationSeconds);
            Assert.Equal(GameplayMessageEndMode.Auto, marker.EndMode);
            Assert.Equal(GameplayMessageOnEndEffects.FadeOut, marker.OnEndEffects);
            Assert.Equal(1f, marker.OnEndSeconds);
            Assert.False(marker.FreezeSimulation);
        }
        Assert.InRange(messages[0].Width, 144.99f, 145.01f);
        Assert.InRange(messages[0].Height, 41.16f, 41.18f);
        Assert.Equal(messages[0].Width, messages[1].Width);
        Assert.Equal(messages[0].Height, messages[1].Height);
        Assert.InRange(messages[2].Width, 168.99f, 169.01f);
        Assert.InRange(messages[2].Height, 56.16f, 56.18f);
    }

    [Fact]
    public void OnlyFirstEligiblePresentationClaimsTheHintsAndDeathOrMenusPauseThem()
    {
        var sequence = new FirstPlayHintSequence(false);
        for (var i = 0; i < 300; i++) Assert.False(sequence.Update(0.1f, false));
        Assert.False(sequence.Started);
        Assert.True(sequence.Update(0.1f, true));
        Assert.True(sequence.Visible);
        for (var i = 0; i < 10; i++) Assert.False(sequence.Update(0.1f, true));
        var elapsed = sequence.ElapsedSeconds;
        for (var i = 0; i < 100; i++) sequence.Update(0.1f, false);
        Assert.Equal(elapsed, sequence.ElapsedSeconds);
        Assert.False(sequence.Visible);
        Assert.False(sequence.Update(0.1f, true));
        Assert.Equal(0, sequence.Index);
        Assert.True(sequence.Visible);
    }

    [Fact]
    public void SequenceFinishesAllThreeMessagesInOrderThenNeverRestarts()
    {
        var sequence = new FirstPlayHintSequence(false);
        sequence.Update(0, true);
        for (var index = 0; index < 3; index++)
        {
            Assert.Equal(index, sequence.Index);
            var frames = 0;
            while (!sequence.Finished && sequence.Index == index && frames < 400)
            {
                Assert.False(sequence.Update(1f / 60f, true));
                frames++;
            }
            Assert.InRange(frames, 299, 302); // four seconds plus the authored one-second end fade
        }
        Assert.True(sequence.Finished);
        Assert.False(sequence.Visible);
        Assert.False(sequence.Update(0.1f, true));
        Assert.False(new FirstPlayHintSequence(true).Update(0.1f, true));
    }

    [Fact]
    public void LeavingBeforeOrAfterStartDoesNotConsumeOrRepeatAnUnseenSequence()
    {
        var sequence = new FirstPlayHintSequence(false);
        sequence.LeaveSession();
        Assert.True(sequence.Update(0, true));
        sequence.LeaveSession();
        Assert.True(sequence.Finished);
        Assert.False(sequence.Update(0.1f, true));
    }

    [Fact]
    public void DesktopFlagSurvivesANewDocumentInstance()
    {
        var path = Path.Combine(Path.GetTempPath(), $"opengarrison-first-play-{Guid.NewGuid():N}.json");
        try
        {
            Assert.False(FirstPlayHintsDocument.Load(path).HasShown);
            new FirstPlayHintsDocument { HasShown = true }.Save(path);
            Assert.True(FirstPlayHintsDocument.Load(path).HasShown);
        }
        finally { File.Delete(path); }
    }

    [Fact]
    public void BrowserFlagUsesPersistentBridgeAndSurvivesPageStoreReinitialization()
    {
        var disk = new Dictionary<string, string>();
        BrowserPreferenceStore.Initialize(disk, (key, value) => disk[key] = value);
        Assert.False(FirstPlayHintsDocument.LoadBrowser().HasShown);
        new FirstPlayHintsDocument { HasShown = true }.SaveBrowser();
        Assert.Contains(FirstPlayHintsDocument.BrowserKey, disk.Keys);
        BrowserPreferenceStore.Initialize(disk, (key, value) => disk[key] = value);
        Assert.True(FirstPlayHintsDocument.LoadBrowser().HasShown);
        Assert.False(new FirstPlayHintSequence(FirstPlayHintsDocument.LoadBrowser().HasShown).Update(0, true));
    }

    [Theory]
    [InlineData(960, 540, 480, 270)]
    [InlineData(640, 480, 200, 140)]
    [InlineData(320, 240, 5, 120)]
    [InlineData(320, 240, 315, 120)]
    public void MessageFollowsHeadWithFifteenPixelGapAndStaysOnScreen(int width, int height, int x, int y)
    {
        var player = new Rectangle(x - 10, y, 20, 30);
        var viewport = new Rectangle(0, 0, width, height);
        foreach (var marker in FirstPlayMessageCatalog.Messages)
        {
            var bounds = FirstPlayHintPlacement.AbovePlayer(marker, player, viewport);
            Assert.Equal(15, player.Top - bounds.Bottom);
            Assert.InRange(bounds.Left, 4, width - bounds.Width - 4);
            Assert.Equal((int)MathF.Round(marker.Width), bounds.Width);
            var moved = FirstPlayHintPlacement.AbovePlayer(marker, player with { Y = y + 30 }, viewport);
            Assert.Equal(30, moved.Top - bounds.Top);
        }
    }
}
