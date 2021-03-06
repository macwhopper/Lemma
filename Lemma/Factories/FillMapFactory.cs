﻿using System; using ComponentBind;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lemma.Components;
using Microsoft.Xna.Framework;
using BEPUphysics.Paths.PathFollowing;
using Lemma.Util;
using BEPUphysics;
using BEPUphysics.BroadPhaseEntries.MobileCollidables;
using BEPUphysics.Constraints.TwoEntity.Motors;
using BEPUphysics.Constraints.TwoEntity.Joints;
using BEPUphysics.Constraints.SolverGroups;

namespace Lemma.Factories
{
	public class FillMapFactory : MapFactory
	{
		public class CoordinateEntry
		{
			public Map.Coordinate Coord;
			public Vector3 Position;
			public float Distance;
		}

		public override Entity Create(Main main, int offsetX, int offsetY, int offsetZ)
		{
			Entity result = base.Create(main, offsetX, offsetY, offsetZ);
			result.Type = "FillMap";
			result.ID = Entity.GenerateID(result, main);
			return result;
		}

		public override void Bind(Entity result, Main main, bool creating = false)
		{
			this.InternalBind(result, main, creating, null, true);
			if (result.GetOrMakeProperty<bool>("Attached", true))
				MapAttachable.MakeAttachable(result, main);

			Property<Entity.Handle> target = result.GetOrMakeProperty<Entity.Handle>("Target");

			Map map = result.Get<Map>();

			Property<float> intervalMultiplier = result.GetOrMakeProperty<float>("IntervalMultiplier", true, 1.0f);

			ListProperty<CoordinateEntry> coords = result.GetOrMakeListProperty<CoordinateEntry>("Coordinates");

			Property<int> index = result.GetOrMakeProperty<int>("FillIndex");

			Action populateCoords = delegate()
			{
				if (coords.Count == 0)
				{
					Entity targetEntity = target.Value.Target;
					if (targetEntity != null && targetEntity.Active)
					{
						Map m = targetEntity.Get<Map>();
						foreach (CoordinateEntry e in map.Chunks.SelectMany(c => c.Boxes.SelectMany(x => x.GetCoords())).Select(delegate(Map.Coordinate y)
						{
							Map.Coordinate z = m.GetCoordinate(map.GetAbsolutePosition(y));
							z.Data = y.Data;
							return new CoordinateEntry { Coord = z, };
						}))
							coords.Add(e);
					}
				}
			};

			if (main.EditorEnabled)
				coords.Clear();
			else
				result.Add(new PostInitialization { populateCoords });

			Property<float> blockLifetime = result.GetOrMakeProperty<float>("BlockLifetime", true, 0.25f);

			float intervalTimer = 0.0f;
			Updater update = new Updater
			{
				delegate(float dt)
				{
					intervalTimer += dt;
					Entity targetEntity = target.Value.Target;
					if (targetEntity != null && targetEntity.Active && index < coords.Count)
					{
						float interval = 0.03f * intervalMultiplier;
						while (intervalTimer > interval && index < coords.Count)
						{
							EffectBlockFactory factory = Factory.Get<EffectBlockFactory>();
							Map m = targetEntity.Get<Map>();
							
							CoordinateEntry entry = coords[index];
							Entity block = factory.CreateAndBind(main);
							entry.Coord.Data.ApplyToEffectBlock(block.Get<ModelInstance>());
							block.GetProperty<bool>("CheckAdjacent").Value = false;
							block.GetProperty<Vector3>("Offset").Value = m.GetRelativePosition(entry.Coord);
							block.GetProperty<bool>("Scale").Value = true;

							block.GetProperty<Vector3>("StartPosition").Value = entry.Position + new Vector3(8.0f, 20.0f, 8.0f) * blockLifetime.Value;
							block.GetProperty<Matrix>("StartOrientation").Value = Matrix.CreateRotationX(0.15f * index) * Matrix.CreateRotationY(0.15f * index);

							block.GetProperty<float>("TotalLifetime").Value = blockLifetime;
							factory.Setup(block, targetEntity, entry.Coord, entry.Coord.Data.ID);
							main.Add(block);

							index.Value++;
							intervalTimer -= interval;
						}
					}
					else
						result.Delete.Execute();
				}
			};
			update.Enabled.Value = index > 0;
			result.Add("Update", update);

			Action fill = delegate()
			{
				if (index > 0 || update.Enabled)
					return; // We're already filling

				Entity targetEntity = target.Value.Target;
				if (targetEntity != null && targetEntity.Active)
				{
					populateCoords();
					Map m = targetEntity.Get<Map>();
					Vector3 focusPoint = main.Camera.Position;
					foreach (CoordinateEntry entry in coords)
					{
						entry.Position = m.GetAbsolutePosition(entry.Coord);
						entry.Distance = (focusPoint - entry.Position).LengthSquared();
					}

					List<CoordinateEntry> coordList = coords.ToList();
					coords.Clear();
					coordList.Sort(new LambdaComparer<CoordinateEntry>((x, y) => x.Distance.CompareTo(y.Distance)));
					foreach (CoordinateEntry e in coordList)
						coords.Add(e);

					update.Enabled.Value = true;
				}
			};

			result.Add("Fill", new Command
			{
				Action = fill
			});

			result.Add("Trigger", new Command<Entity>
			{
				Action = delegate(Entity p)
				{
					fill();
				}
			});
		}

		public override void AttachEditorComponents(Entity result, Main main)
		{
			base.AttachEditorComponents(result, main);

			MapAttachable.AttachEditorComponents(result, main, result.Get<Model>().Color);

			EntityConnectable.AttachEditorComponents(result, result.GetOrMakeProperty<Entity.Handle>("Target"));
		}
	}
}
