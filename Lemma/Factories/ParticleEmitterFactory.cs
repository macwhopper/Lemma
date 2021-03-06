﻿using System; using ComponentBind;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using Microsoft.Xna.Framework;
using Lemma.Components;
using System.IO;
using System.Xml.Serialization;

namespace Lemma.Factories
{
	public class ParticleEmitterFactory : Factory<Main>
	{
		public ParticleEmitterFactory()
		{
			this.Color = new Vector3(0.4f, 1.0f, 0.4f);
		}

		public override Entity Create(Main main)
		{
			Entity result = new Entity(main, "ParticleEmitter");

			result.Add("Transform", new Transform());
			result.Add("ParticleEmitter", new ParticleEmitter());

			return result;
		}

		public override void Bind(Entity result, Main main, bool creating = false)
		{
			this.SetMain(result, main);
			Transform transform = result.Get<Transform>();
			ParticleEmitter emitter = result.Get<ParticleEmitter>();
			emitter.Add(new Binding<Vector3>(emitter.Position, transform.Position));

			if (result.GetOrMakeProperty<bool>("Attach", true))
				MapAttachable.MakeAttachable(result, main);
		}

		public override void AttachEditorComponents(Entity result, Main main)
		{
			base.AttachEditorComponents(result, main);
			Model editorModel = result.Get<Model>("EditorModel");
			ParticleEmitter emitter = result.Get<ParticleEmitter>();
			Property<bool> editorSelected = result.GetOrMakeProperty<bool>("EditorSelected");
			editorSelected.Serialize = false;
			editorModel.Add(new Binding<bool>(editorModel.Enabled, () => !editorSelected || emitter.ParticleType.Value == null, editorSelected, emitter.ParticleType));

			MapAttachable.AttachEditorComponents(result, main, result.Get<Model>().Color);
		}
	}
}