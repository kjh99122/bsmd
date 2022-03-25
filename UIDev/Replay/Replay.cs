﻿using BossMod;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Numerics;

namespace UIDev
{
    public class Replay
    {
        public class TimeRange
        {
            public DateTime Start;
            public DateTime End;

            public TimeRange(DateTime start = new(), DateTime end = new()) { Start = start; End = end; }
            public override string ToString() => $"{(End - Start).TotalSeconds:f2}";
        }

        public class Cast
        {
            public ActionID ID;
            public float ExpectedCastTime;
            public TimeRange Time = new();
            public Participant? Target;
            public Vector3 Location; // if target is non-null, corresponds to target's position at cast start
        }

        public class ActionTarget
        {
            public Participant? Target;
            public Vector4 PosRot;
            public ActionEffects Effects;
        }

        public class Action
        {
            public ActionID ID;
            public DateTime Timestamp;
            public Participant? Source;
            public Vector4 SourcePosRot;
            public Participant? MainTarget;
            public Vector4 MainTargetPosRot;
            public List<ActionTarget> Targets = new();
        }

        public class Participant
        {
            public uint InstanceID;
            public uint OID;
            public ActorType Type;
            public string Name = "";
            public TimeRange Existence = new();
            public List<TimeRange> Targetable = new();
            public List<Cast> Casts = new();
            public bool HasAnyActions;
            public bool HasAnyStatuses;
            public bool IsTargetOfAnyActions;
        }

        public class Status
        {
            public uint ID;
            public Participant? Target;
            public Participant? Source;
            public float InitialDuration;
            public TimeRange Time = new();
            public ushort StartingExtra;
        }

        public class Tether
        {
            public uint ID;
            public Participant? Source;
            public Participant? Target;
            public TimeRange Time = new();
        }

        public class Icon
        {
            public uint ID;
            public Participant? Target;
            public DateTime Timestamp;
        }

        public class EnvControl
        {
            public uint Feature;
            public byte Index;
            public uint State;
            public DateTime Timestamp;
        }

        public class Encounter
        {
            public uint InstanceID;
            public uint OID;
            public TimeRange Time = new();
            public ushort Zone;
            public Dictionary<uint, List<Participant>> Participants = new(); // key = oid
            public int FirstAction;
            public int FirstStatus;
            public int FirstTether;
            public int FirstIcon;
            public int FirstEnvControl;
        }

        public string Path = "";
        public List<ReplayOps.Operation> Ops = new();
        public List<Participant> Participants = new();
        public List<Action> Actions = new();
        public List<Status> Statuses = new();
        public List<Tether> Tethers = new();
        public List<Icon> Icons = new();
        public List<EnvControl> EnvControls = new();
        public List<Encounter> Encounters = new();

        public IEnumerable<Action> EncounterActions(Encounter e) => Actions.Skip(e.FirstAction).TakeWhile(a => a.Timestamp <= e.Time.End);
        public IEnumerable<Status> EncounterStatuses(Encounter e) => Statuses.Skip(e.FirstStatus).TakeWhile(s => s.Time.Start <= e.Time.End);
        public IEnumerable<Tether> EncounterTethers(Encounter e) => Tethers.Skip(e.FirstTether).TakeWhile(t => t.Time.Start <= e.Time.End);
        public IEnumerable<Icon> EncounterIcons(Encounter e) => Icons.Skip(e.FirstIcon).TakeWhile(i => i.Timestamp <= e.Time.End);
        public IEnumerable<EnvControl> EncounterEnvControls(Encounter e) => EnvControls.Skip(e.FirstEnvControl).TakeWhile(ec => ec.Timestamp <= e.Time.End);
    }
}
