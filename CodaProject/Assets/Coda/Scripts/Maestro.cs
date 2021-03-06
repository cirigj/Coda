using UnityEngine;
using System.Collections;
using System.Collections.Generic;

namespace Coda {

    /// <summary>
    /// Used to quantify if a frame is on beat, early, or late.
    /// </summary>
    public enum Timing {
        early = -1,
        onBeat = 0,
        late = 1,
    }

	/// <summary>
	/// Maestro singleton class. Tracks the beatmap and directs subscribed MusicBehaviour listeners.
	/// </summary>
	[RequireComponent(typeof(AudioSource))]
	public class Maestro : MonoBehaviour {

	    public static Maestro current = null;

		public TextAsset beatmapFile;

        [Tooltip("Link beat tracking to progression Audioclip in the Maestro")]
        public bool audioClipTracking = false;
		private BeatMap beatmap;
	    private AudioSource _audio;

		public bool loopAudio;

		public delegate void OnBeatDelegate();
		public OnBeatDelegate onBeat;
        public OnBeatDelegate lateOnBeat;

		List<MusicBehaviour> listeners;

		private bool _audioClipExists;
        private bool _beatMapExists;

		private double _beatTimer;
        private Beat _prevBeat;
		private Beat _nextBeat;
		private int _beatIndex;
		private bool _songEnded;
        private bool _beatFrame;
        private float _averageTimeBetweenBeats = -1f;
		private float _minTimeBetweenBeats = -1f;
		private float _maxTimeBetweenBeats = -1f;

        /// <summary>
        /// Returns the average time between beats.
        /// </summary>
        public float averageTimeBetweenBeats {
            get {
                if (_averageTimeBetweenBeats == -1f) {
                    if (beatmap.beats.Count > 1) {
                        _averageTimeBetweenBeats = (float)((beatmap.beats[beatmap.beats.Count - 1].timeStamp - beatmap.beats[0].timeStamp) / (beatmap.beats.Count - 1));
                    }
                }
                return _averageTimeBetweenBeats;
            }
        }

		/// <summary>
		/// Returns the minimum time between beats.
		/// </summary>
		public float minTimeBetweenBeats {
			get {
				if (_minTimeBetweenBeats == -1f) {
					if (beatmap.beats.Count > 1) {
						float minTime = Mathf.Infinity;
						for (int i = 1; i < beatmap.beats.Count; i++) {
							if ((float)(beatmap.beats[i].timeStamp - beatmap.beats[i-1].timeStamp) < minTime) {
								minTime = (float)(beatmap.beats[i].timeStamp - beatmap.beats[i-1].timeStamp);
							}
						}
						_minTimeBetweenBeats = minTime;
					}
				}
				return _minTimeBetweenBeats;
			}
		}

		/// <summary>
		/// Returns the minimum time between beats.
		/// </summary>
		public float maxTimeBetweenBeats {
			get {
				if (_maxTimeBetweenBeats == -1f) {
					if (beatmap.beats.Count > 1) {
						float maxTime = 0f;
						for (int i = 1; i < beatmap.beats.Count; i++) {
							if ((float)(beatmap.beats[i].timeStamp - beatmap.beats[i-1].timeStamp) > maxTime) {
								maxTime = (float)(beatmap.beats[i].timeStamp - beatmap.beats[i-1].timeStamp);
							}
						}
						_maxTimeBetweenBeats = maxTime;
					}
				}
				return _maxTimeBetweenBeats;
			}
		}

        /// <summary>
        /// Returns the time until the next beat as a float.
        /// </summary>
        public float timeUntilNextBeat {
            get {
                if (_nextBeat == default(Beat)) {
                    return -1f;
                }
                else {
                    return (float)(_nextBeat.timeStamp - _beatTimer);
                }
            }
        }
        
        /// <summary>
        /// Returns the time until the next beat as a double-precision float.
        /// </summary>
        public double timeUntilNextBeat_Double {
            get {
                if (_nextBeat == default(Beat)) {
                    return -1;
                }
                else {
                    return _nextBeat.timeStamp - _beatTimer;
                }
            }
        }

        /// <summary>
        /// Returns the time until the previous beat as a float.
        /// </summary>
        public float timeSincePreviousBeat {
            get {
                if (_prevBeat == default(Beat)) {
                    return (float)_beatTimer;
                }
                else {
                    return (float)(_prevBeat.timeStamp - _beatTimer);
                }
            }
        }

        /// <summary>
        /// Returns the time until the previous beat as a double-precision float.
        /// </summary>
        public double timeSincePreviousBeat_Double {
            get {
                if (_prevBeat == default(Beat)) {
                    return _beatTimer;
                }
                else {
                    return _prevBeat.timeStamp - _beatTimer;
                }
            }
        }

        /// <summary>
        /// Returns the time to the closest beat as a float.
        /// </summary>
        public float timeToClosestBeat {
            get { return Mathf.Min(timeSincePreviousBeat, timeUntilNextBeat); }
        }

        /// <summary>
        /// Returns the time to the closest beat as a double-precision float.
        /// </summary>
        public double timeToClosestBeat_Double {
            get { return System.Math.Min(timeSincePreviousBeat_Double, timeUntilNextBeat_Double); }
        }

		/// <summary>
		/// Gets the previous beat.
		/// </summary>
		public Beat previousBeat {
			get { return _prevBeat; }
		}

		/// <summary>
		/// Gets the next beat.
		/// </summary>
		public Beat nextBeat {
			get { return _nextBeat; }
		}

		/// <summary>
		/// Gets the beat which is closest to the current frame.
		/// </summary>
		public Beat closestBeat {
			get {
				if (Mathf.Abs((float)(_nextBeat.timeStamp - _beatTimer)) < Mathf.Abs((float)(_prevBeat.timeStamp - _beatTimer))) {
					return _nextBeat;
				}
				else {
					return _prevBeat;
				}
			}
		}

        /// <summary>
        /// Gets the index of the next beat.
        /// </summary>
        public int nextBeatIndex {
            get { return _beatIndex; }
        }

        /// <summary>
        /// Gets the index of the previous beat.
        /// </summary>
        public int prevBeatIndex {
            get { return _beatIndex - 1; }
        }

        /// <summary>
        /// Gets the index of the beat which is closest to the current frame.
        /// </summary>
        public int closestBeatIndex {
            get {
                if (Mathf.Abs((float)(_nextBeat.timeStamp - _beatTimer)) < Mathf.Abs((float)(_prevBeat.timeStamp - _beatTimer))) {
                    return nextBeatIndex;
                }
                else {
                    return prevBeatIndex;
                }
            }
        }

        /// <summary>
        /// Makes a coroutine wait until the next beat.
        /// </summary>
        public WaitForSeconds WaitForNextBeat () {
            return new WaitForSeconds(timeUntilNextBeat);
        }

        /// <summary>
        /// Checks if the current frame is on a beat.
        /// </summary>
        public bool IsOnBeat () {
            return _beatFrame;
        }

        /// <summary>
        /// Checks if the current frame is on a beat.
        /// </summary>
        /// <param name="timing">Will return the timing of the current beat (Timing.early, Timing.onBeat, or Timing.late).</param>
        public bool IsOnBeat (out Timing timing) {
            if (_beatFrame) {
                timing = Timing.onBeat;
                return true;
            }
            float prevDist = Mathf.Abs((float)(_beatTimer - _prevBeat.timeStamp));
            float nextDist = Mathf.Abs((float)(_beatTimer - _nextBeat.timeStamp));
            if (nextDist < prevDist) {
                timing = Timing.early;
            }
            else {
                timing = Timing.late;
            }
            return false;
        }

        /// <summary>
        /// Checks if the current frame is within a given number of seconds from the closest beat.
        /// </summary>
        /// <param name="precision">How many seconds from the beat count as "on beat".</param>
        public bool IsOnBeat (float precision) {
            if (_beatFrame) {
                return true;
            }
            float prevDist = Mathf.Abs((float)(_beatTimer - _prevBeat.timeStamp));
            float nextDist = Mathf.Abs((float)(_beatTimer - _nextBeat.timeStamp));
            if (Mathf.Min(nextDist, prevDist) <= precision) {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if the current frame is within a given number of seconds from the closest beat.
        /// </summary>
        /// <param name="precision">How many seconds from the beat count as "on beat".</param>
        /// <param name="timeDifference">Returns the number of seconds to the closest beat.</param>
        public bool IsOnBeat (float precision, out float timeDifference) {
            if (_beatFrame) {
                timeDifference = 0f;
                return true;
            }
            float prevDist = Mathf.Abs((float)(_beatTimer - _prevBeat.timeStamp));
            float nextDist = Mathf.Abs((float)(_beatTimer - _nextBeat.timeStamp));
            timeDifference = Mathf.Min(prevDist, nextDist);
            return (timeDifference <= precision);
        }

        /// <summary>
        /// Checks if the current frame is within a given number of seconds from the closest beat.
        /// </summary>
        /// <param name="precision">How many seconds from the beat count as "on beat".</param>
        /// <param name="timing">Will return the timing of the current beat (Timing.early, Timing.onBeat, or Timing.late).</param>
        public bool IsOnBeat (float precision, out Timing timing) {
            if (_beatFrame) {
                timing = Timing.onBeat;
                return true;
            }
            float prevDist = Mathf.Abs((float)(_beatTimer - _prevBeat.timeStamp));
            float nextDist = Mathf.Abs((float)(_beatTimer - _nextBeat.timeStamp));
            if (nextDist < prevDist) {
                timing = Timing.early;
            }
            else {
                timing = Timing.late;
            }
            if (Mathf.Min(nextDist, prevDist) <= precision) {
                return true;
            }
            return false;
        }

        /// <summary>
        /// Checks if the current frame is within a given number of seconds from the closest beat.
        /// </summary>
        /// <param name="precision">How many seconds from the beat count as "on beat".</param>
        /// <param name="timeDifference">Returns the number of seconds to the closest beat.</param>
        /// <param name="timing">Will return the timing of the current beat (Timing.early, Timing.onBeat, or Timing.late).</param>
        public bool IsOnBeat (float precision, out float timeDifference, out Timing timing) {
            if (_beatFrame) {
                timeDifference = 0f;
                timing = Timing.onBeat;
                return true;
            }
            float prevDist = Mathf.Abs((float)(_beatTimer - _prevBeat.timeStamp));
            float nextDist = Mathf.Abs((float)(_beatTimer - _nextBeat.timeStamp));
            if (nextDist < prevDist) {
                timing = Timing.early;
            }
            else {
                timing = Timing.late;
            }
            timeDifference = Mathf.Min(prevDist, nextDist);
            return (timeDifference <= precision);
        }

	    void Awake() {
	        if (current == null) {
	            current = this;
	        }
            _beatMapExists = true;
			_audioClipExists = true;
	        _audio = GetComponent<AudioSource>();
			if (_audio.clip == null) {
				_audioClipExists = false;
				Debug.LogError("Maestro: No Audio Clip!");
			}
			else {
				if ("BeatMap_" + _audio.clip.name.Replace(".mp3", "") != beatmapFile.name) {
					Debug.LogWarning("Maestro: Audio Clip and Beatmap File name mismatch!");
				}
			}
            _beatIndex = 0;
			onBeat = OnBeat;
            lateOnBeat = LateOnBeat;
			listeners = new List<MusicBehaviour>();
			beatmap = BeatMapSerializer.BeatMapReader.ReadBeatMap(beatmapFile);
	    }

		void Start () {
			if (_audioClipExists) {
	        	_audio.Play();
            }
			if (!StartTracking()) {
				_beatMapExists = false;
				Debug.LogError("Maestro: Beatmap has zero beats!");
			}
		}
		
		void Update () {
			if (_beatMapExists) {
				if (TrackBeats()) {
					onBeat();
				}
			}
		}

        void LateUpdate () {
            if (_beatFrame) {
                lateOnBeat();
            }
        }

		/// <summary>
		/// Starts tracking the audio and beatmap.
		/// </summary>
		/// <returns><c>true</c>, if tracking was started successfully, <c>false</c> otherwise.</returns>
		bool StartTracking () {
			if (beatmap == null || beatmap.beats.Count == 0) {
				return false;
			}
			_songEnded = false;
			_nextBeat = beatmap.beats[0];
			_beatTimer = 0.0;
			return true;
		}

		/// <summary>
		/// Tracks the beats.
		/// </summary>
		/// <returns><c>true</c>, if there was a beat this frame, <c>false</c> otherwise.</returns>
		bool TrackBeats () {
			if (!(_songEnded && !loopAudio) && !audioClipTracking) {
				_beatTimer += Time.deltaTime;
			}
            else if (!(_songEnded && !loopAudio) && audioClipTracking) {
                if (!_audioClipExists) {
                    Debug.LogError("Audio clip missing! Add an audio clip to the Maestro AudioSource or uncheck Audio Clip Tracking in the Inspector");
                    _beatTimer += Time.deltaTime;
                }
                else {
                    double beatTimeElapsed = _audio.time - _beatTimer;
                    _beatTimer += beatTimeElapsed;
                }
            }
			else {
                _beatFrame = false;
				return false;
			}
			if (_songEnded) {
				if (_beatTimer >= (double)beatmap.songLength) {
                    if (_audioClipExists) {
                        _audio.Stop();
                    }
					StartTracking();
                    if (_audioClipExists) {
                        _audio.Play();
                    }
				}
			}
			else if (_nextBeat.timeStamp <= _beatTimer) {
				_beatIndex++;
				if (_beatIndex == beatmap.beats.Count) {
					_songEnded = true;
                    if (loopAudio) {
                        _nextBeat = beatmap.beats[0];
                    }
				}
				else {
                    _prevBeat = _nextBeat;
					_nextBeat = beatmap.beats[_beatIndex % beatmap.beats.Count];
				}
                _beatFrame = true;
				return true;
			}
            _beatFrame = false;
			return false;
		}

		/// <summary>
		/// Activates during every beat on the beatmap.
		/// </summary>
		void OnBeat () {
			//Debug.Log("Beat.");
		}

        void LateOnBeat () {
            //Debug.Log("Late Update Beat.");
        }

		/// <summary>
		/// Subscribe the specified listener to the Maestro.
		/// </summary>
		/// <param name="listener">Listener (should be a subclass of MusicBehaviour).</param>
		public void Subscribe (MusicBehaviour listener) {
			if (!listeners.Contains(listener)) {
				listeners.Add(listener);
				onBeat += listener.OnBeat;
                lateOnBeat += listener.LateOnBeat;
			}
		}

		/// <summary>
		/// Unsubscribe the specified listener to the Maestro.
		/// </summary>
		/// <param name="listener">Listener (should be a subclass of MusicBehaviour).</param>
		public void Unsubscribe (MusicBehaviour listener) {
			if (listeners.Contains(listener)) {
				listeners.Remove(listener);
				onBeat -= listener.OnBeat;
                lateOnBeat -= listener.LateOnBeat;
			}
		}
	}

}
