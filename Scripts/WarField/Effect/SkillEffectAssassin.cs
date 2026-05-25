using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Assassin;

namespace WarField
{
    using ED = EffectDefines;

    public class SkillEffectAssassin : EffectBase
    {
#region public parameters

#endregion

#region private parameters

        [SerializeField] private ParticleSystem _spark;

        private Gradient _colorGrad; //color over life 使用的渐变色
        private ParticleSystem.ColorOverLifetimeModule _sparkColorOverLifetime;
        private AssassinIndividualData.IndividualDataType _curSkillType = AssassinIndividualData.IndividualDataType.MIN;
#endregion

#region private parameters' get set
        public override EffectDefines.EffectType gs_effectType
        {
            get { return ED.EffectType.ASSASSINSKILL; }
        }
#endregion

#region Unity callbacks

        protected override void Awake()
        {
            _effectType = ED.EffectType.ASSASSINSKILL;
            base.Awake();

            Gradient originalGrad = _spark.colorOverLifetime.color.gradient;
            _colorGrad = new Gradient();
            _colorGrad.SetKeys(originalGrad.colorKeys, originalGrad.alphaKeys);
            _sparkColorOverLifetime = _spark.colorOverLifetime;
        }

#endregion

#region public functions

#endregion

#region private functions

        protected override void OnActiveEffect(object value)
        {
            AssassinIndividualData.IndividualDataType skillType = (AssassinIndividualData.IndividualDataType)value;
            if(skillType == _curSkillType) //不需要再次设置颜色
                return;

            _curSkillType = skillType;
            switch (_curSkillType)
            {
                case AssassinIndividualData.IndividualDataType.CRIT:
                    //_colorGrad.SetKeys(new GradientColorKey[]{new GradientColorKey(new Color32(255, 160, 54, 255), 0.0f)}, _colorGrad.alphaKeys);
                    //_sparkColorOverLifetime.color = new ParticleSystem.MinMaxGradient(_colorGrad);
                    break;
                case AssassinIndividualData.IndividualDataType.SUNDERDEFENSES:

                    break;
                case AssassinIndividualData.IndividualDataType.ASSASSINATE:
                    _colorGrad.SetKeys(new GradientColorKey[]{new GradientColorKey(new Color32(255, 59, 59, 255), 0.0f)}, _colorGrad.alphaKeys);
                    _sparkColorOverLifetime.color = new ParticleSystem.MinMaxGradient(_colorGrad);
                    break;
                default:
                    break;
            }
        }

#endregion
    }
}

