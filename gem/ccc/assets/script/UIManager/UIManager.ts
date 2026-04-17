
import { _decorator, Animation, animation, Asset, Component, instantiate, Node, TTFFont, Prefab, resources, RichText, primitives, AudioSource, builtinResMgr, Canvas, Scene, Pool, error, input, Input, EventTouch, Vec2, Vec3, Camera, find } from 'cc';
import { BundleManager } from '../BundleManager/BundleManager';

export class UIManager {
    private parent:Node|null = null;

    public CurrPageName:string = "";
    public CurrPage:Node|null = null;

    private currBorader:Node|null = null;
    public currboraderName:string = "";

    public static instance:UIManager;
    static get Instance():UIManager {
        if(this.instance==null)
            this.instance=new UIManager();
        return this.instance;
    }

    public Init(parent:Node) {
        this.parent = parent;
    }
    
    public async OpenPage(pageName:string, bundleName:string) {
        if(this.CurrPageName == pageName) {
            console.warn("当前页面已打开:"+pageName);
            return;
        }

        let old = this.CurrPage;

        let bundle = await BundleManager.Instance.LoadAssetsFromBundle(bundleName, pageName) as Prefab;
        let node = instantiate(bundle);
        node.setParent(this.parent);
        this.CurrPage = node;
        this.CurrPageName = pageName;

        if(old) {
            old.destroy();
        }
    }

    public ClosePage() {
        if(this.CurrPage) {
            this.CurrPage.destroy();
            this.CurrPage = null;
            this.CurrPageName = "";
        }
    }

    public async OpenBorader(boraderName:string, bundleName:string) {
        if(this.currboraderName == boraderName) {
            console.warn("当前界面已打开:"+boraderName);
            return;
        }

        let old = this.currBorader;

        let bundle = await BundleManager.Instance.LoadAssetsFromBundle(bundleName, boraderName) as Prefab;
        let node = instantiate(bundle);
        node.setParent(this.CurrPage);
        this.currBorader = node;
        this.currboraderName = boraderName;

        if(old) {
            old.destroy();
        }
    }

    public CloseBorader() {
        if(this.currBorader) {
            this.currBorader.destroy();
            this.currBorader = null;
            this.currboraderName = "";
        }
    }
}