/*
 * 新建 AudioManager.ts
 * author: Hotaru
 * 2024/04/06
 */
import { _decorator, AudioClip, AudioSource, Component, director, Node } from 'cc';
import { BundleManager } from '../BundleManager/BundleManager';

export class AudioManager
{
    private parent:Node|null = null;
    private static instance:AudioManager|null = null;
    private audioSource:AudioSource|null = null;
    private static audioClips:AudioClip[]=[];

    public Init(parent:Node) {
        let audioMgr = new Node();
        audioMgr.name = '__audioMgr__';
        director.getScene()!.addChild(audioMgr);
        director.addPersistRootNode(audioMgr);
        this.audioSource = audioMgr.addComponent(AudioSource);
    }
    
    public static get Instance() {
        if(null == this.instance) {
            this.instance=new AudioManager();
        }
        return this.instance;
    }

    public get AudioSource() {
        return this.audioSource;
    }

    public async PlaySound(sound: AudioClip | string, volume: number = 1.0) {
        try {
            let clip = sound instanceof AudioClip ? sound : await this.FoundClips(sound);
            if (this.audioSource != null && clip != null) {
                this.audioSource.stop();
                this.audioSource.clip = clip;
                this.audioSource.play();
                this.audioSource.volume = volume;
            }
        }
       catch(error) {
            console.error('AudioManager 下 PlaySound 错误 err: ',error);
       }
    }

    public async PlayerOnShot(sound: AudioClip | string, volume: number = 1.0) {
        try {
            let clip = sound instanceof AudioClip ? sound : await this.FoundClips(sound);
            if (this.audioSource != null && clip != null) {
                this.audioSource.playOneShot(clip, volume);
            }
        }
        catch(error) {
            console.error('AudioManager 下 PlayerOnShot 错误 err: ',error);
        }
    }

    public Stop() {
        this.audioSource?.stop();
    }

    public Pause() {
        this.audioSource?.pause();
    }

    public Resume() {
        this.audioSource?.play();
    }

    private async FoundClips(_name:string) {
        let ads=_name.split('/');
        for(let i=0 ; i<AudioManager.audioClips.length ; i++) {
            if(AudioManager.audioClips[i].name == ads[1]) {
                return AudioManager.audioClips[i];
            }
        }

        let clip = await BundleManager.Instance.LoadAssetsFromBundle(ads[0], ads[1]) as AudioClip;
        AudioManager.audioClips.push(clip);

        return clip;
    }
}


