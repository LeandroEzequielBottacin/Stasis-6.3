using System.Collections;
using System.Threading;
using System.Threading.Tasks;
using UnityEngine;
using UnityEngine.Animations.Rigging;

namespace Puzzle_Elements.IK.Scripts
{
    public class BlendConstraint1 : MonoBehaviour
    {
        #region properties
        public BlendConstraint blendConstraint;
        public string triggerLeft = "TriggerLeft";
        public string triggerRight = "TriggerRight";
        public float lerpTime = 1f;
        public float holdTime = 4f;
        public bool useCoroutine;
        public bool useBlendSimple;
        #endregion

        private CancellationTokenSource cancellationTokenSource;

        private void OnTriggerEnter(Collider other)
        {
            if(other.name == triggerLeft)
            {
                //StartCoroutine(BlendWeightsCoroutine(1f, 0f));
                BlendWeights(0f);

            }
            else if(other.name == triggerRight)
            {
                //StartCoroutine(BlendWeightsCoroutine(0f,1f));
                BlendWeights(1f);
            }
        }
        async void BlendWeights(float targetWeight)
        {
            //Cancel the previous task if it`s still running
            cancellationTokenSource?.Cancel();
            cancellationTokenSource = new CancellationTokenSource();
            CancellationToken token = cancellationTokenSource.Token;

            float elapsedTime = 0f;

            float startPositionWeight = blendConstraint.data.positionWeight;
            float startRotationWeight = blendConstraint.data.rotationWeight;
            float startWeight = blendConstraint.weight;

            // Lerp blendConstraint.weight from 0 to 1
            while (elapsedTime <lerpTime)
            {
                if (token.IsCancellationRequested)
                {
                    elapsedTime += Time.deltaTime;
                    float blend = Mathf.Clamp01(elapsedTime / lerpTime);

                    blendConstraint.data.positionWeight = Mathf.Lerp(startPositionWeight, targetWeight, blend);
                    blendConstraint.data.rotationWeight = Mathf.Lerp(startRotationWeight, targetWeight, blend);
                    blendConstraint.weight = Mathf.Lerp(startWeight, 1f, blend);

                    await Task.Yield();
                }
            }
            blendConstraint.data.positionWeight = targetWeight;
            blendConstraint.data.rotationWeight = targetWeight;
            blendConstraint.weight = 1f;

            // Hold the weight at 1 for holdTime seconds
            try
            {
                await Task.Delay((int)(holdTime * 1000), token);
            }
            catch (TaskCanceledException)
            {
                return;
            }

            // Lerp blendConstraint.weight back to 0
            elapsedTime = 0;
            startWeight = blendConstraint.weight;

            while(elapsedTime < lerpTime)
            {
                if (token.IsCancellationRequested) return;

                elapsedTime += Time.deltaTime;
                float blend = Mathf.Clamp01(elapsedTime / lerpTime);
                blendConstraint.weight = Mathf.Lerp(startWeight, 0, blend);

                await Task.Yield();
            }

            blendConstraint.weight = 0f;
        }
        async void BlendWeightsNoReset(float targetWeight)
        {
            float elapsedTime = 0f;

            float startPositionWeight = blendConstraint.data.positionWeight;
            float startRotationWeight = blendConstraint.data.rotationWeight;
            float startweight = blendConstraint.weight;

            // Lerp blendConstraint.weight from 0 to 1
            while(elapsedTime < lerpTime)
            {
                elapsedTime += Time.deltaTime;
                float blend = Mathf.Clamp01(elapsedTime / lerpTime);

                blendConstraint.data.positionWeight = Mathf.Lerp(startPositionWeight, targetWeight, blend);
                blendConstraint.data.rotationWeight = Mathf.Lerp(startRotationWeight, targetWeight, blend);
                blendConstraint.weight = Mathf.Lerp(startweight, 1f, blend);

                //yield return null;
                await Task.Yield();
            }

            blendConstraint.data.positionWeight = targetWeight;
            blendConstraint.data.rotationWeight = targetWeight;
            blendConstraint.weight = 1f;

            // Yield return new waitForSeconds(holdTime);
            await Task.Delay((int)(holdTime * 1000));

            // Lerp blendConstraint.weight back to 0
            elapsedTime = 0f;
            startweight = blendConstraint.weight;

            while(elapsedTime < lerpTime)
            {
                elapsedTime += Time.deltaTime;
                float blend = Mathf.Clamp01(elapsedTime / lerpTime);

                blendConstraint.weight = Mathf.Lerp(startweight, 0f, blend);

                // Yield return null
                await Task.Yield();
            }
            blendConstraint.weight = 0f;
        }
        IEnumerator BlendWeightsCoroutine(float start,float end)
        {
            for (float time = 0; time <1; time += Time.deltaTime/lerpTime)
            {
                blendConstraint.data.positionWeight = Mathf.Lerp(start, end, time);
                blendConstraint.data.rotationWeight = Mathf.Lerp(start, end, time);
                blendConstraint.weight = Mathf.Lerp(0, 1, time);
                yield return null;
            }
            blendConstraint.data.positionWeight = end;
            blendConstraint.data.rotationWeight = end;
            blendConstraint.weight = 1;

            //Hold the weight at 1 for holdTime seconds
            yield return new WaitForSeconds(holdTime);

            for (float time = 1; time > 0; time -= Time.deltaTime/lerpTime)
            {
                blendConstraint.data.positionWeight = Mathf.Lerp(start, end, time);
                blendConstraint.data.rotationWeight = Mathf.Lerp(start, end, time);
                blendConstraint.weight = Mathf.Lerp(0, 1, time);
                yield return null;
            }
            blendConstraint.data.positionWeight = end;
            blendConstraint.data.rotationWeight = end;
            blendConstraint.weight = 0;
        }
        //StartCoroutine
        IEnumerator BasicCoroutine()
        {
            yield return new WaitForEndOfFrame();
            yield return new WaitForSeconds(1);
        }
        async Task BasicAsync()
        {
            await Task.Yield();
            await Task.Delay(1000);
        } 
    }
}
