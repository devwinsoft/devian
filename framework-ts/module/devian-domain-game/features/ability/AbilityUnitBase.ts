import { AbilityBase } from './AbilityBase';

export abstract class AbilityUnitBase extends AbilityBase {
    abstract get unitId(): string;
}
