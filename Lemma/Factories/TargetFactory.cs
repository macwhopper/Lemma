﻿using System; using ComponentBind;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Lemma.Components;
using Microsoft.Xna.Framework;

namespace Lemma.Factories
{
	public class TargetFactory : Factory<Main>
	{
		public static ListProperty<Transform> Positions = new ListProperty<Transform>();

		public TargetFactory()
		{
			this.Color = new Vector3(1.0f, 0.4f, 0.4f);
		}

		public override Entity Create(Main main)
		{
			Entity result = new Entity(main, "Target");

			result.Add("Transform", new Transform());

			return result;
		}

		public override void Bind(Entity result, Main main, bool creating = false)
		{
			PlayerTrigger trigger = result.GetOrCreate<PlayerTrigger>("Trigger");

			base.Bind(result, main, creating);

			Transform transform = result.Get<Transform>();
			transform.Editable = true;
			transform.Enabled.Editable = true;

			TargetFactory.Positions.Add(transform);
			result.Add(new CommandBinding(result.Delete, delegate()
			{
				TargetFactory.Positions.Remove(transform);
			}));

			Property<bool> deleteWhenReached = result.GetOrMakeProperty<bool>("DeleteWhenReached", true, true);
			trigger.Add(new TwoWayBinding<bool>(deleteWhenReached, trigger.Enabled));
			trigger.Add(new Binding<Vector3>(trigger.Position, transform.Position));
			trigger.Add(new CommandBinding<Entity>(trigger.PlayerEntered, delegate(Entity p)
			{
				result.Add(new Animation
				(
					new Animation.Delay(0.0f),
					new Animation.Execute(result.Delete)
				));
			}));

			if (result.GetOrMakeProperty<bool>("Attach", true))
				MapAttachable.MakeAttachable(result, main);
		}

		public override void AttachEditorComponents(Entity result, Main main)
		{
			base.AttachEditorComponents(result, main);

			PlayerTrigger.AttachEditorComponents(result, main, this.Color);

			Model model = new Model();
			model.Filename.Value = "Models\\sphere";
			model.Color.Value = this.Color;
			model.Scale.Value = new Vector3(0.5f);
			model.IsInstanced.Value = false;
			model.Editable = false;
			model.Serialize = false;

			result.Add("EditorModel3", model);

			model.Add(new Binding<Matrix>(model.Transform, result.Get<Transform>().Matrix));

			MapAttachable.AttachEditorComponents(result, main);
		}
	}
}
