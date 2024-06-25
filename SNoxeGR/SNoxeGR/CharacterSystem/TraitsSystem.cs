using System;
using System.Collections.Generic;
using System.Linq;
using Vintagestory.API.Client;
using Vintagestory.API.Common;
using Vintagestory.API.Config;
using Vintagestory.API.Server;
using Vintagestory.GameContent;

namespace SNoxeGR.CharacterSystem;

public class TraitsSystem : ModSystem
{
    private const string TraitsConfig = "game:config/traits.json";
    private const string CharClassesConfig = "game:config/characterclasses.json";

    private const string StatTraitPrefix = "trait-";
    private const string RepeatSuffix = "-repeat";
    private const string FireAtSuffix = "-fireat";
    private const string KeyTraits = "traits";

    private const double UpdateInterval = 100;
    
    private ILogger _log;
    private Dictionary<string, EntityPlayer> _players;
    private Dictionary<string, List<ITraitAttributeBehaviour>> _traitBehaviours;
    
    public List<CharacterClass> CharacterClasses = new();
    public List<Trait> Traits = new();
    public Dictionary<string, CharacterClass> CharacterClassesByCode = new();
    public Dictionary<string, Trait> TraitsByCode = new();
    
    public override void Start(ICoreAPI api)
    {
        _log = api.Logger;
        base.Start(api);
    }

    public override void StartServerSide(ICoreServerAPI api)
    {
        base.StartServerSide(api);
        api.Event.PlayerJoin += Event_PlayerJoinServer;
        api.Event.PlayerLeave += Event_PlayerLeaveServer;
        api.Event.ServerRunPhase(EnumServerRunPhase.ModsAndConfigReady, () => LoadConfig(api));
        api.Event.Timer(ProcessPlayersTraits, UpdateInterval);
    }

    public override void StartClientSide(ICoreClientAPI api)
    {
        base.StartClientSide(api);
        api.Event.BlockTexturesLoaded += () => LoadConfig(api);
    }

    public override void Dispose()
    {
        Traits = new List<Trait>();
        CharacterClasses = new List<CharacterClass>();
        TraitsByCode = new Dictionary<string, Trait>();
        CharacterClassesByCode = new Dictionary<string, CharacterClass>();
        base.Dispose();
    }

    public void RegisterTraitAttributeBehaviour(ITraitAttributeBehaviour x)
    {
        if (!_traitBehaviours.ContainsKey(x.PropertyName()))
        {
            _traitBehaviours[x.PropertyName()] = new List<ITraitAttributeBehaviour>();
        }
        
        _traitBehaviours[x.PropertyName()].Add(x);
    }

    public void ApplyTrait(EntityPlayer player, Trait trait)
    {
        var wa = player.WatchedAttributes;
        var arr = wa.GetStringArray(KeyTraits, Array.Empty<string>());
        var list = new List<string>(arr);
        if (list.Contains(trait.Code))
        {
            return;
        }
        
        list.Add(trait.Code);
        wa.SetStringArray(KeyTraits, list.ToArray());

        foreach (var attr in trait.Attributes)
        {
            player.Stats.Set(attr.Key, StatTraitPrefix + trait.Code, (float)attr.Value, true);
        }

        if (!trait.IsTemp()) return;
        var time = player.World.Calendar.ElapsedSeconds;
        wa.SetInt(StatTraitPrefix+trait.Code+RepeatSuffix, trait.Repeat);
        wa.SetLong(StatTraitPrefix+trait.Code+FireAtSuffix, trait.Duration+time);
    }

    public void ApplyTrait(EntityPlayer player, string code)
    {
        var trait = TraitsByCode[code];
        if (trait == null)
        {
            throw new ApplicationException($"Trait by code {code} does not exist");
        }
        
        ApplyTrait(player, trait);
    }

    public void RemoveTrait(EntityPlayer player, string code)
    {
        var trait = TraitsByCode[code];
        if (trait == null)
        {
            throw new ApplicationException($"Trait by code {code} does not exist");
        }
        
        RemoveTrait(player, trait);
    }

    public void RemoveTrait(EntityPlayer player, Trait trait)
    {
        var wa = player.WatchedAttributes;
        var arr = wa.GetStringArray(KeyTraits, Array.Empty<string>());
        var list = new List<string>(arr);
        if (!list.Contains(trait.Code))
        {
            return;
        }

        list.Remove(trait.Code);
        wa.SetStringArray(KeyTraits, list.ToArray());
        
        foreach (var attr in trait.Attributes)
        {
            player.Stats.Remove(attr.Key, StatTraitPrefix + trait.Code);
        }
        
        wa.RemoveAttribute(StatTraitPrefix+trait.Code+RepeatSuffix);
        wa.RemoveAttribute(StatTraitPrefix+trait.Code+FireAtSuffix);
    }

    public bool HasTrait(EntityPlayer player, string code)
    {
        var arr = player.WatchedAttributes.GetStringArray(KeyTraits, Array.Empty<string>());
        var list = new List<string>(arr);
        return list.Contains(code);
    }

    private void LoadConfig(ICoreAPI api)
    {
        Traits = api.Assets.Get(TraitsConfig).ToObject<List<Trait>>();
        CharacterClasses = api.Assets.Get(CharClassesConfig).ToObject<List<CharacterClass>>();
        foreach (var trait in Traits)
        {
            TraitsByCode[trait.Code] = trait;   
        }
        foreach (var characterClass in CharacterClasses)
        {
            CharacterClassesByCode[characterClass.Code] = characterClass;
        }
    }

    private void Event_PlayerJoinServer(IServerPlayer player)
    {
        _players[player.PlayerUID] = player.Entity;
    }
    
    private void Event_PlayerLeaveServer(IServerPlayer player)
    {
        if (!_players.ContainsKey(player.PlayerUID))
        {
            return;
        }

        _players.Remove(player.PlayerUID);
    }

    private void ProcessPlayersTraits()
    {
        foreach (var kv in _players)
        {
            UpdateTraits(kv.Value);    
        }
    }

    private void UpdateTraits(EntityPlayer player)
    {
        var currentTime = player.World.Calendar.ElapsedSeconds;
        var wa = player.WatchedAttributes;
        var tArr = wa.GetStringArray(KeyTraits);
        var list = new List<string>(tArr).Select(x => TraitsByCode[x]).ToList();
        
        foreach (var trait in list)
        {
            var fireAt = wa.GetLong(StatTraitPrefix + trait.Code + FireAtSuffix);
            if (currentTime < fireAt)
            {
                continue;
            }
            
            foreach (var attr in trait.Attributes)
            {
                if (!_traitBehaviours.TryGetValue(attr.Key, out var value)) continue;
                foreach (var b in value)
                {
                    b.Process(player, (float)attr.Value);
                }
            }
            
            var repeat = wa.GetInt(StatTraitPrefix + trait.Code + RepeatSuffix) - 1;
            if (repeat <= 0)
            {
                RemoveTrait(player, trait);
                continue;
            }
            
            wa.SetInt(StatTraitPrefix+trait.Code+RepeatSuffix, repeat);
            wa.SetLong(StatTraitPrefix+trait.Code+FireAtSuffix, trait.Duration+currentTime);
        }
    }
}