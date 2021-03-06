﻿using System; using ComponentBind;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Lemma.Components;

namespace Lemma.Factories
{
	public class CloudFactory : Factory<Main>
	{
		public CloudFactory()
		{
			this.Color = new Vector3(0.9f, 0.7f, 0.5f);
		}

		public override Entity Create(Main main)
		{
			Entity result = new Entity(main, "Cloud");

			Transform transform = new Transform();
			result.Add("Transform", transform);

			ModelAlpha clouds = new ModelAlpha();
			clouds.Filename.Value = "Models\\clouds";
			clouds.DrawOrder.Value = -9;
			result.Add("Clouds", clouds);

			return result;
		}

		public override void Bind(Entity result, Main main, bool creating = false)
		{
			base.Bind(result, main, creating);
			result.CannotSuspendByDistance = true;

			ModelAlpha clouds = result.Get<ModelAlpha>("Clouds");
			clouds.CullBoundingBox.Value = false;
			clouds.DisableCulling.Value = true;

			Property<float> height = result.GetOrMakeProperty<float>("Height", true, 1.0f);
			result.Add(new Binding<float>(clouds.GetFloatParameter("Height"), height));

			Property<Vector2> velocity = result.GetOrMakeProperty<Vector2>("Velocity", true, Vector2.One);
			result.Add(new Binding<Vector2>(clouds.GetVector2Parameter("Velocity"), x => x * (1.0f / 60.0f), velocity));

			result.Add(new CommandBinding(main.ReloadedContent, delegate()
			{
				height.Reset();
				velocity.Reset();
			}));

			Property<float> startDistance = result.GetOrMakeProperty<float>("StartDistance", true, 50);
			clouds.Add(new Binding<float>(clouds.GetFloatParameter("StartDistance"), startDistance));
		}
	}
}
